using GameInterface.Services.Players;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

public interface IPendingLocalOwnerConsequenceRegistry
{
    void DeferQuestFail(string controllerId, string questTypeKey, byte proof);
    void FlushReady(IPlayerManager playerManager, Action<string, string, byte> deliver);
    void ClearAll();
    void Restore(string controllerId, string questTypeKey, byte proof);
    IReadOnlyCollection<(string ControllerId, string QuestTypeKey, byte Proof)> Snapshot();
}

internal sealed class PendingLocalOwnerConsequenceRegistry : IPendingLocalOwnerConsequenceRegistry
{
    private readonly Dictionary<string, List<(string QuestTypeKey, byte Proof)>> pendingQuestFailByController = new();

    public void DeferQuestFail(string controllerId, string questTypeKey, byte proof)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(questTypeKey)) return;

        if (!pendingQuestFailByController.TryGetValue(controllerId, out var entries))
        {
            entries = new List<(string, byte)>();
            pendingQuestFailByController[controllerId] = entries;
        }

        entries.Add((questTypeKey, proof));
    }

    public void FlushReady(IPlayerManager playerManager, Action<string, string, byte> deliver)
    {
        if (pendingQuestFailByController.Count == 0) return;

        var readyControllerIds = new List<string>();
        foreach (var controllerId in pendingQuestFailByController.Keys)
        {
            if (playerManager.TryGetPlayer(controllerId, out var player) && playerManager.IsCampaignReady(player))
            {
                readyControllerIds.Add(controllerId);
            }
        }

        foreach (var controllerId in readyControllerIds)
        {
            var entries = pendingQuestFailByController[controllerId];
            pendingQuestFailByController.Remove(controllerId);

            foreach (var (questTypeKey, proof) in entries)
            {
                deliver(controllerId, questTypeKey, proof);
            }
        }
    }

    public void ClearAll() => pendingQuestFailByController.Clear();

    public void Restore(string controllerId, string questTypeKey, byte proof) => DeferQuestFail(controllerId, questTypeKey, proof);

    public IReadOnlyCollection<(string ControllerId, string QuestTypeKey, byte Proof)> Snapshot()
    {
        var snapshot = new List<(string, string, byte)>();
        foreach (var kvp in pendingQuestFailByController)
        {
            foreach (var (questTypeKey, proof) in kvp.Value)
            {
                snapshot.Add((kvp.Key, questTypeKey, proof));
            }
        }
        return snapshot;
    }
}
