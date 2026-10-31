using System.Collections.Generic;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Reconciles per-client battle results against the server's current mission membership. A kept host's report still counts
/// after it left, and once the wait for members expired the host's report alone concludes the battle.
/// </summary>
public interface IBattleCompletionTracker
{
    /// <summary>Records a member's report and tries to conclude; <paramref name="hostKept"/> is the assignment's kept flag.</summary>
    bool TryRecordResult(
        string instanceId,
        string controllerId,
        BattleState battleState,
        int reportEpoch,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true,
        bool hostKept = false);

    /// <summary>Tries to conclude from the reports already in; <paramref name="hostKept"/> is the assignment's kept flag.</summary>
    bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true,
        bool hostKept = false);

    bool TryConcludeAbandoned(
        string instanceId,
        out BattleState concludedState,
        out int hostEpoch,
        out int memberCount);

    void HostAssigned(string instanceId, string hostControllerId, int hostEpoch);

    /// <summary>Stops waiting for unmatched members, so the host's report alone concludes the battle (Q7).</summary>
    void StopWaitingForMembers(string instanceId);

    void MemberDeparted(string instanceId, string controllerId);
    void ResetMember(string instanceId, string controllerId, bool isFirstMember);
    void Clear(string instanceId);
}

/// <inheritdoc cref="IBattleCompletionTracker"/>
public class BattleCompletionTracker : IBattleCompletionTracker
{
    private readonly object gate = new object();
    private readonly Dictionary<string, Dictionary<string, BattleResultReport>> reports = new();
    private readonly Dictionary<string, HashSet<string>> participants = new();
    private readonly Dictionary<string, BattleResultReport> authoritativeReports = new();
    // Battles whose wait for their members expired, so the host's report concludes them alone.
    private readonly HashSet<string> membersNotAwaited = new();

    public bool TryRecordResult(
        string instanceId,
        string controllerId,
        BattleState battleState,
        int reportEpoch,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true,
        bool hostKept = false)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId) ||
            currentMembers == null ||
            (battleState != BattleState.AttackerVictory
             && battleState != BattleState.DefenderVictory
             && battleState != BattleState.DefenderPullBack))
        {
            return false;
        }

        lock (gate)
        {
            var members = new HashSet<string>(currentMembers);
            if (!members.Contains(controllerId))
                return false;

            GetParticipants(instanceId).UnionWith(members);

            if (!reports.TryGetValue(instanceId, out var instanceReports))
            {
                instanceReports = new Dictionary<string, BattleResultReport>();
                reports[instanceId] = instanceReports;
            }

            var report = new BattleResultReport(controllerId, battleState, reportEpoch);
            instanceReports[controllerId] = report;
            RememberAuthoritativeReport(instanceId, controllerId, hostControllerId, hostEpoch, report);

            return canConclude &&
                TryConclude(instanceId, instanceReports, members, hostControllerId, hostEpoch, hostKept, out concludedState);
        }
    }

    public bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true,
        bool hostKept = false)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || currentMembers == null)
            return false;

        lock (gate)
        {
            if (!reports.TryGetValue(instanceId, out var instanceReports))
            {
                return false;
            }

            var members = new HashSet<string>(currentMembers);
            GetParticipants(instanceId).UnionWith(members);
            if (instanceReports.TryGetValue(hostControllerId ?? string.Empty, out var hostReport))
                RememberAuthoritativeReport(instanceId, hostControllerId, hostControllerId, hostEpoch, hostReport);

            return canConclude &&
                TryConclude(instanceId, instanceReports, members, hostControllerId, hostEpoch, hostKept, out concludedState);
        }
    }

    public bool TryConcludeAbandoned(
        string instanceId,
        out BattleState concludedState,
        out int hostEpoch,
        out int memberCount)
    {
        concludedState = BattleState.None;
        hostEpoch = 0;
        memberCount = 0;
        if (string.IsNullOrEmpty(instanceId))
            return false;

        lock (gate)
        {
            if (!reports.TryGetValue(instanceId, out var instanceReports) ||
                !participants.TryGetValue(instanceId, out var instanceParticipants) ||
                instanceParticipants.Count == 0 ||
                !authoritativeReports.TryGetValue(instanceId, out var authoritativeReport))
            {
                return false;
            }

            if (!membersNotAwaited.Contains(instanceId) &&
                !AllReportsMatch(instanceReports, instanceParticipants, authoritativeReport.BattleState))
                return false;

            concludedState = authoritativeReport.BattleState;
            hostEpoch = authoritativeReport.HostEpoch;
            memberCount = instanceParticipants.Count;
            return true;
        }
    }

    public void HostAssigned(string instanceId, string hostControllerId, int hostEpoch)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(hostControllerId))
            return;

        lock (gate)
        {
            // A new hosting generation must report at its own epoch. Retained reports still count toward
            // participant consensus, but cannot stand in for the new host's final state and siege snapshot.
            if (authoritativeReports.TryGetValue(instanceId, out var report) &&
                (report.ControllerId != hostControllerId || report.HostEpoch != hostEpoch))
            {
                authoritativeReports.Remove(instanceId);
            }
        }
    }

    public void StopWaitingForMembers(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        lock (gate)
            membersNotAwaited.Add(instanceId);
    }

    public void ResetMember(string instanceId, string controllerId, bool isFirstMember)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId))
            return;

        lock (gate)
        {
            if (isFirstMember)
                ClearInternal(instanceId);

            if (participants.TryGetValue(instanceId, out var instanceParticipants))
                instanceParticipants.Remove(controllerId);
            if (reports.TryGetValue(instanceId, out var instanceReports))
                instanceReports.Remove(controllerId);
            if (authoritativeReports.TryGetValue(instanceId, out var authoritativeReport) &&
                authoritativeReport.ControllerId == controllerId)
            {
                authoritativeReports.Remove(instanceId);
            }
        }
    }

    public void MemberDeparted(string instanceId, string controllerId)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId))
            return;

        lock (gate)
        {
            if (reports.TryGetValue(instanceId, out var instanceReports) &&
                instanceReports.ContainsKey(controllerId))
            {
                return;
            }

            if (participants.TryGetValue(instanceId, out var instanceParticipants))
                instanceParticipants.Remove(controllerId);
        }
    }

    public void Clear(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        lock (gate)
            ClearInternal(instanceId);
    }

    // The current host's report at its epoch, matched by every current member unless the wait for them expired; a kept
    // host may have left already.
    private bool TryConclude(
        string instanceId,
        Dictionary<string, BattleResultReport> instanceReports,
        HashSet<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        bool hostKept,
        out BattleState concludedState)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(hostControllerId) ||
            (!currentMembers.Contains(hostControllerId) && !hostKept) ||
            !instanceReports.TryGetValue(hostControllerId, out var hostReport) ||
            hostReport.HostEpoch != hostEpoch)
        {
            return false;
        }

        if (!membersNotAwaited.Contains(instanceId) && !AllReportsMatch(instanceReports, currentMembers, hostReport.BattleState))
            return false;

        concludedState = hostReport.BattleState;
        return true;
    }

    // Whether every given member reported the given state.
    private static bool AllReportsMatch(Dictionary<string, BattleResultReport> instanceReports, IEnumerable<string> members,
        BattleState battleState)
    {
        foreach (var member in members)
        {
            if (!instanceReports.TryGetValue(member, out var memberReport) || memberReport.BattleState != battleState)
                return false;
        }

        return true;
    }

    private HashSet<string> GetParticipants(string instanceId)
    {
        if (!participants.TryGetValue(instanceId, out var instanceParticipants))
        {
            instanceParticipants = new HashSet<string>();
            participants[instanceId] = instanceParticipants;
        }

        return instanceParticipants;
    }

    private void RememberAuthoritativeReport(
        string instanceId,
        string controllerId,
        string hostControllerId,
        int hostEpoch,
        BattleResultReport report)
    {
        if (!string.IsNullOrEmpty(hostControllerId) &&
            controllerId == hostControllerId &&
            report.HostEpoch == hostEpoch)
        {
            authoritativeReports[instanceId] = report;
        }
    }

    private void ClearInternal(string instanceId)
    {
        reports.Remove(instanceId);
        participants.Remove(instanceId);
        authoritativeReports.Remove(instanceId);
        membersNotAwaited.Remove(instanceId);
    }

    private readonly struct BattleResultReport
    {
        public string ControllerId { get; }
        public BattleState BattleState { get; }
        public int HostEpoch { get; }

        public BattleResultReport(string controllerId, BattleState battleState, int hostEpoch)
        {
            ControllerId = controllerId;
            BattleState = battleState;
            HostEpoch = hostEpoch;
        }
    }
}
