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
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        out BattleState concludedState);

    bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        out BattleState concludedState);

    void RemoveMember(string instanceId, string controllerId, bool isInstanceEmpty);
}

/// <inheritdoc cref="IBattleCompletionTracker"/>
public class BattleCompletionTracker : IBattleCompletionTracker
{
    private readonly object gate = new object();
    private readonly Dictionary<string, Dictionary<string, BattleState>> reports = new();
    private readonly HashSet<string> concludedInstances = new();

    public bool TryRecordResult(
        string instanceId,
        string controllerId,
        BattleState battleState,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        out BattleState concludedState)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId) ||
            string.IsNullOrEmpty(hostControllerId) || currentMembers == null ||
            (battleState != BattleState.AttackerVictory && battleState != BattleState.DefenderVictory))
        {
            return false;
        }

        lock (gate)
        {
            if (concludedInstances.Contains(instanceId))
                return false;

            var members = new HashSet<string>(currentMembers);
            if (!members.Contains(controllerId) || !members.Contains(hostControllerId))
                return false;

            if (!reports.TryGetValue(instanceId, out var instanceReports))
            {
                instanceReports = new Dictionary<string, BattleState>();
                reports[instanceId] = instanceReports;
            }

            RemoveDepartedReports(instanceReports, members);
            instanceReports[controllerId] = battleState;

            return TryConclude(instanceId, instanceReports, members, hostControllerId, out concludedState);
        }
    }

    public bool TryReconcile(
        string instanceId,
        IReadOnlyCollection<string> currentMembers,
        string hostControllerId,
        out BattleState concludedState)
    {
        concludedState = BattleState.None;
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(hostControllerId) || currentMembers == null)
            return false;

        lock (gate)
        {
            if (concludedInstances.Contains(instanceId) ||
                !reports.TryGetValue(instanceId, out var instanceReports))
            {
                return false;
            }

            var members = new HashSet<string>(currentMembers);
            RemoveDepartedReports(instanceReports, members);
            return TryConclude(instanceId, instanceReports, members, hostControllerId, out concludedState);
        }
    }

    public void RemoveMember(string instanceId, string controllerId, bool isInstanceEmpty)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;

        lock (gate)
        {
            if (isInstanceEmpty)
            {
                reports.Remove(instanceId);
                concludedInstances.Remove(instanceId);
                return;
            }

            if (reports.TryGetValue(instanceId, out var instanceReports))
                instanceReports.Remove(controllerId);
        }
    }

    private static void RemoveDepartedReports(
        Dictionary<string, BattleState> instanceReports,
        HashSet<string> currentMembers)
    {
        var departed = new List<string>();
        foreach (var controllerId in instanceReports.Keys)
        {
            if (!currentMembers.Contains(controllerId))
                departed.Add(controllerId);
        }

        foreach (var controllerId in departed)
            instanceReports.Remove(controllerId);
    }

    private bool TryConclude(
        string instanceId,
        Dictionary<string, BattleState> instanceReports,
        HashSet<string> currentMembers,
        string hostControllerId,
        out BattleState concludedState)
    {
        concludedState = BattleState.None;
        if (!currentMembers.Contains(hostControllerId) ||
            !instanceReports.TryGetValue(hostControllerId, out var hostState))
        {
            return false;
        }

        foreach (var member in currentMembers)
        {
            if (!instanceReports.TryGetValue(member, out var memberState) || memberState != hostState)
                return false;
        }

        concludedInstances.Add(instanceId);
        concludedState = hostState;
        return true;
    }
}
