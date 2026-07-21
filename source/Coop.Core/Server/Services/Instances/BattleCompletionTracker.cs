using System.Collections.Generic;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.Instances;

/// <summary>Reconciles per-client battle results against the server's current mission membership.</summary>
public interface IBattleCompletionTracker
{
    bool TryRecordResult(
        string instanceId,
        string controllerId,
        BattleState battleState,
        int reportEpoch,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true);

    bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true);

    bool TryConcludeAbandoned(
        string instanceId,
        out BattleState concludedState,
        out int hostEpoch,
        out int memberCount);

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
    private readonly HashSet<string> concludedInstances = new();

    public bool TryRecordResult(
        string instanceId,
        string controllerId,
        BattleState battleState,
        int reportEpoch,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId) ||
            currentMembers == null ||
            (battleState != BattleState.AttackerVictory && battleState != BattleState.DefenderVictory))
        {
            return false;
        }

        lock (gate)
        {
            if (concludedInstances.Contains(instanceId))
                return false;

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
                TryConclude(instanceId, instanceReports, members, hostControllerId, hostEpoch, out concludedState);
        }
    }

    public bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState,
        bool canConclude = true)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || currentMembers == null)
            return false;

        lock (gate)
        {
            if (concludedInstances.Contains(instanceId) ||
                !reports.TryGetValue(instanceId, out var instanceReports))
            {
                return false;
            }

            var members = new HashSet<string>(currentMembers);
            GetParticipants(instanceId).UnionWith(members);
            if (instanceReports.TryGetValue(hostControllerId ?? string.Empty, out var hostReport))
                RememberAuthoritativeReport(instanceId, hostControllerId, hostControllerId, hostEpoch, hostReport);

            return canConclude &&
                TryConclude(instanceId, instanceReports, members, hostControllerId, hostEpoch, out concludedState);
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
            if (concludedInstances.Contains(instanceId) ||
                !reports.TryGetValue(instanceId, out var instanceReports) ||
                !participants.TryGetValue(instanceId, out var instanceParticipants) ||
                instanceParticipants.Count == 0 ||
                !authoritativeReports.TryGetValue(instanceId, out var authoritativeReport))
            {
                return false;
            }

            foreach (var controllerId in instanceParticipants)
            {
                if (!instanceReports.TryGetValue(controllerId, out var report) ||
                    report.BattleState != authoritativeReport.BattleState)
                {
                    return false;
                }
            }

            concludedInstances.Add(instanceId);
            concludedState = authoritativeReport.BattleState;
            hostEpoch = authoritativeReport.HostEpoch;
            memberCount = instanceParticipants.Count;
            return true;
        }
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

    private bool TryConclude(
        string instanceId,
        Dictionary<string, BattleResultReport> instanceReports,
        HashSet<string> currentMembers,
        string hostControllerId,
        int hostEpoch,
        out BattleState concludedState)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(hostControllerId) ||
            !currentMembers.Contains(hostControllerId) ||
            !instanceReports.TryGetValue(hostControllerId, out var hostReport) ||
            hostReport.HostEpoch != hostEpoch)
        {
            return false;
        }

        foreach (var member in currentMembers)
        {
            if (!instanceReports.TryGetValue(member, out var memberReport) ||
                memberReport.BattleState != hostReport.BattleState)
                return false;
        }

        concludedInstances.Add(instanceId);
        concludedState = hostReport.BattleState;
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
        concludedInstances.Remove(instanceId);
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
