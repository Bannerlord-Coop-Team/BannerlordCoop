using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Interfaces;

public interface IAwaitingAlternativeSolutionTroopsRegistry
{
    void Deposit(string ownerControllerId, TroopRoster troops);
    bool TryGet(string ownerControllerId, out TroopRoster troops);
    void Withdraw(string ownerControllerId, TroopRoster troops);
    void Clear(string ownerControllerId);
    void ClearAll();
    void Restore(string ownerControllerId, TroopRoster troops);
    IReadOnlyCollection<(string OwnerControllerId, TroopRoster Troops)> Snapshot();
}

internal sealed class AwaitingAlternativeSolutionTroopsRegistry : IAwaitingAlternativeSolutionTroopsRegistry
{
    private readonly Dictionary<string, TroopRoster> troopsByOwnerControllerId = new();

    public void Deposit(string ownerControllerId, TroopRoster troops)
    {
        if (string.IsNullOrEmpty(ownerControllerId) || troops == null || troops.Count == 0) return;

        if (!troopsByOwnerControllerId.TryGetValue(ownerControllerId, out var existing))
        {
            existing = TroopRoster.CreateDummyTroopRoster();
            troopsByOwnerControllerId[ownerControllerId] = existing;
        }

        existing.Add(troops);
    }

    public bool TryGet(string ownerControllerId, out TroopRoster troops)
    {
        troops = null;
        if (string.IsNullOrEmpty(ownerControllerId)) return false;

        return troopsByOwnerControllerId.TryGetValue(ownerControllerId, out troops) && troops.Count > 0;
    }

    public void Withdraw(string ownerControllerId, TroopRoster troops)
    {
        if (string.IsNullOrEmpty(ownerControllerId) || troops == null || troops.Count == 0) return;
        if (!troopsByOwnerControllerId.TryGetValue(ownerControllerId, out var existing)) return;

        var toRemove = new List<(CharacterObject Character, int Number)>();
        foreach (var element in troops.GetTroopRoster())
        {
            toRemove.Add((element.Character, element.Number));
        }

        foreach (var (character, number) in toRemove)
        {
            var removeCount = Math.Min(number, existing.GetTroopCount(character));
            if (removeCount > 0)
            {
                existing.AddToCounts(character, -removeCount, false, 0, 0, true);
            }
        }

        if (existing.Count == 0)
        {
            troopsByOwnerControllerId.Remove(ownerControllerId);
        }
    }

    public void Clear(string ownerControllerId)
    {
        if (string.IsNullOrEmpty(ownerControllerId)) return;
        troopsByOwnerControllerId.Remove(ownerControllerId);
    }

    public void ClearAll()
    {
        troopsByOwnerControllerId.Clear();
    }

    public void Restore(string ownerControllerId, TroopRoster troops)
    {
        if (string.IsNullOrEmpty(ownerControllerId) || troops == null || troops.Count == 0) return;
        troopsByOwnerControllerId[ownerControllerId] = troops;
    }

    public IReadOnlyCollection<(string OwnerControllerId, TroopRoster Troops)> Snapshot()
    {
        var snapshot = new List<(string, TroopRoster)>(troopsByOwnerControllerId.Count);
        foreach (var kvp in troopsByOwnerControllerId)
        {
            snapshot.Add((kvp.Key, kvp.Value));
        }
        return snapshot;
    }
}
