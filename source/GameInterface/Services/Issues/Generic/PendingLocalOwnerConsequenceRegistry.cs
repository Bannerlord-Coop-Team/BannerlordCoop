using GameInterface.Services.Players;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

internal static class PendingLocalOwnerConsequenceRegistry
{
    private static readonly Dictionary<string, List<(string QuestTypeKey, byte Proof)>> PendingQuestFailByController = new();

    internal static void DeferQuestFail(string controllerId, string questTypeKey, byte proof)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(questTypeKey)) return;

        if (!PendingQuestFailByController.TryGetValue(controllerId, out var entries))
        {
            entries = new List<(string, byte)>();
            PendingQuestFailByController[controllerId] = entries;
        }

        entries.Add((questTypeKey, proof));
    }

    internal static void FlushConnected(IPlayerManager playerManager, Action<string, string, byte> deliver)
    {
        if (PendingQuestFailByController.Count == 0) return;

        var readyControllerIds = new List<string>();
        foreach (var controllerId in PendingQuestFailByController.Keys)
        {
            if (playerManager.TryGetPlayer(controllerId, out var player) && playerManager.IsConnected(player))
            {
                readyControllerIds.Add(controllerId);
            }
        }

        foreach (var controllerId in readyControllerIds)
        {
            var entries = PendingQuestFailByController[controllerId];
            PendingQuestFailByController.Remove(controllerId);

            foreach (var (questTypeKey, proof) in entries)
            {
                deliver(controllerId, questTypeKey, proof);
            }
        }
    }

    internal static void ClearAllForTests() => PendingQuestFailByController.Clear();
}
