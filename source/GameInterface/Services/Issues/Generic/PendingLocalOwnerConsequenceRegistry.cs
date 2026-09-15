using GameInterface.Services.Players;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

public interface IPendingLocalOwnerConsequenceRegistry
{
    void DeferQuestFail(string controllerId, string questTypeKey, byte proof);
    void FlushReady(IPlayerManager playerManager, Action<string, long, string, byte> deliver);
    void Ack(string controllerId, long obligationId);
    void ClearAll();
    void Restore(string controllerId, long obligationId, string questTypeKey, byte proof);
    IReadOnlyCollection<(string ControllerId, long ObligationId, string QuestTypeKey, byte Proof)> Snapshot();
}

internal sealed class PendingLocalOwnerConsequenceRegistry : IPendingLocalOwnerConsequenceRegistry
{
    private readonly Dictionary<string, List<(long ObligationId, string QuestTypeKey, byte Proof)>> pendingQuestFailByController = new();
    private long nextObligationId = 1;

    public void DeferQuestFail(string controllerId, string questTypeKey, byte proof)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(questTypeKey)) return;

        if (!pendingQuestFailByController.TryGetValue(controllerId, out var entries))
        {
            entries = new List<(long, string, byte)>();
            pendingQuestFailByController[controllerId] = entries;
        }

        entries.Add((nextObligationId++, questTypeKey, proof));
    }

    public void FlushReady(IPlayerManager playerManager, Action<string, long, string, byte> deliver)
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
            foreach (var (obligationId, questTypeKey, proof) in pendingQuestFailByController[controllerId])
            {
                deliver(controllerId, obligationId, questTypeKey, proof);
            }
        }
    }

    public void Ack(string controllerId, long obligationId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (!pendingQuestFailByController.TryGetValue(controllerId, out var entries)) return;

        entries.RemoveAll(e => e.ObligationId == obligationId);
        if (entries.Count == 0) pendingQuestFailByController.Remove(controllerId);
    }

    public void ClearAll() => pendingQuestFailByController.Clear();

    public void Restore(string controllerId, long obligationId, string questTypeKey, byte proof)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(questTypeKey)) return;

        if (!pendingQuestFailByController.TryGetValue(controllerId, out var entries))
        {
            entries = new List<(long, string, byte)>();
            pendingQuestFailByController[controllerId] = entries;
        }

        entries.Add((obligationId, questTypeKey, proof));
        if (obligationId >= nextObligationId) nextObligationId = obligationId + 1;
    }

    public IReadOnlyCollection<(string ControllerId, long ObligationId, string QuestTypeKey, byte Proof)> Snapshot()
    {
        var snapshot = new List<(string, long, string, byte)>();
        foreach (var kvp in pendingQuestFailByController)
        {
            foreach (var (obligationId, questTypeKey, proof) in kvp.Value)
            {
                snapshot.Add((kvp.Key, obligationId, questTypeKey, proof));
            }
        }
        return snapshot;
    }
}
