using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Replacement for vanilla <c>IssueManager._awaitingAlternativeSolutionTroops</c> (a single flat, non-per-owner
/// roster). Keyed by the owning peer's <c>ControllerId</c> rather than <see cref="TaleWorlds.CampaignSystem.Hero"/>
/// - by the time troops reach this point, <c>IssueBase.IssueFinalized()</c> has already cleared the issue's own
/// state, so the connection identity is the only durable key left.
/// </summary>
internal static class AwaitingAlternativeSolutionTroopsRegistry
{
    private static readonly Dictionary<string, TroopRoster> TroopsByOwnerControllerId = new();

    /// <summary>Additive, not a replace, in case more than one of this owner's issues is awaiting return at once.</summary>
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

    /// <summary>Replace, not additive - used only while rehydrating from save data.</summary>
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
