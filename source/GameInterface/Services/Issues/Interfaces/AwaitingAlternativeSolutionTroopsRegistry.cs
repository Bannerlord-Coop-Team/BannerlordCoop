using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Interfaces;

internal static class AwaitingAlternativeSolutionTroopsRegistry
{
    private static readonly Dictionary<string, TroopRoster> TroopsByOwnerControllerId = new();

    public static void Deposit(string ownerControllerId, TroopRoster troops)
    {
        if (string.IsNullOrEmpty(ownerControllerId) || troops == null || troops.Count == 0) return;

        if (!TroopsByOwnerControllerId.TryGetValue(ownerControllerId, out var existing))
        {
            existing = TroopRoster.CreateDummyTroopRoster();
            TroopsByOwnerControllerId[ownerControllerId] = existing;
        }

        existing.Add(troops);
    }

    public static bool TryGet(string ownerControllerId, out TroopRoster troops)
    {
        troops = null;
        if (string.IsNullOrEmpty(ownerControllerId)) return false;

        return TroopsByOwnerControllerId.TryGetValue(ownerControllerId, out troops) && troops.Count > 0;
    }

    public static void Clear(string ownerControllerId)
    {
        if (string.IsNullOrEmpty(ownerControllerId)) return;
        TroopsByOwnerControllerId.Remove(ownerControllerId);
    }

    public static void ClearAll()
    {
        TroopsByOwnerControllerId.Clear();
    }

    public static void Restore(string ownerControllerId, TroopRoster troops)
    {
        if (string.IsNullOrEmpty(ownerControllerId) || troops == null || troops.Count == 0) return;
        TroopsByOwnerControllerId[ownerControllerId] = troops;
    }

    public static IReadOnlyCollection<(string OwnerControllerId, TroopRoster Troops)> Snapshot()
    {
        var snapshot = new List<(string, TroopRoster)>(TroopsByOwnerControllerId.Count);
        foreach (var kvp in TroopsByOwnerControllerId)
        {
            snapshot.Add((kvp.Key, kvp.Value));
        }
        return snapshot;
    }
}
