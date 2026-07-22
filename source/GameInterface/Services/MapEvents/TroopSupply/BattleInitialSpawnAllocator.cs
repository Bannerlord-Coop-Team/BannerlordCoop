using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>One party's capacity and stable deployment authority for initial-spawn allocation.</summary>
public sealed class BattleInitialSpawnParty
{
    public string PartyId { get; }
    public string AuthorityId { get; }
    public BattleSideEnum Side { get; }
    public int Capacity { get; }
    public bool IsDirectPlayerParty { get; }

    public BattleInitialSpawnParty(
        string partyId,
        string authorityId,
        BattleSideEnum side,
        int capacity,
        bool isDirectPlayerParty = false)
    {
        PartyId = partyId;
        AuthorityId = authorityId ?? string.Empty;
        Side = side;
        Capacity = Math.Max(0, capacity);
        IsDirectPlayerParty = isDirectPlayerParty;
    }
}

/// <summary>Allocates one global initial-spawn budget across sides, authorities, and parties.</summary>
public interface IBattleInitialSpawnAllocator
{
    IReadOnlyDictionary<string, int> Allocate(int battleSize, IReadOnlyList<BattleInitialSpawnParty> parties);
}

/// <inheritdoc cref="IBattleInitialSpawnAllocator"/>
public class BattleInitialSpawnAllocator : IBattleInitialSpawnAllocator
{
    private const float MaximumBattleSideRatio = 0.75f;

    private sealed class AllocationItem
    {
        public string Key;
        public int Capacity;
        public int Allocated;
        public long Remainder;
        public bool IsDirectPlayerParty;
    }

    public IReadOnlyDictionary<string, int> Allocate(int battleSize, IReadOnlyList<BattleInitialSpawnParty> parties)
    {
        if (parties == null) throw new ArgumentNullException(nameof(parties));

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var party in parties)
        {
            if (party == null) throw new ArgumentException("Party allocation entries cannot be null.", nameof(parties));
            if (string.IsNullOrEmpty(party.PartyId)) throw new ArgumentException("Party ids cannot be empty.", nameof(parties));
            if (result.ContainsKey(party.PartyId)) throw new ArgumentException($"Duplicate party id '{party.PartyId}'.", nameof(parties));
            result[party.PartyId] = 0;
        }

        var defenders = parties.Where(party => party.Side == BattleSideEnum.Defender && party.Capacity > 0).ToArray();
        var attackers = parties.Where(party => party.Side == BattleSideEnum.Attacker && party.Capacity > 0).ToArray();
        int defenderCapacity = SumCapacity(defenders);
        int attackerCapacity = SumCapacity(attackers);

        AllocateSideSlots(battleSize, defenderCapacity, attackerCapacity, out var defenderSlots, out var attackerSlots);
        AllocateSide(defenders, defenderSlots, result);
        AllocateSide(attackers, attackerSlots, result);

        return result;
    }

    private static void AllocateSide(
        IReadOnlyList<BattleInitialSpawnParty> parties,
        int slots,
        IDictionary<string, int> result)
    {
        if (slots <= 0 || parties.Count == 0) return;

        var authorityGroups = parties
            .GroupBy(party => party.AuthorityId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        var authorityItems = authorityGroups
            .Select(group => new AllocationItem { Key = group.Key, Capacity = SumCapacity(group) })
            .ToArray();
        var authorityAllocations = AllocateProportionally(authorityItems, slots);

        foreach (var group in authorityGroups)
        {
            var partyItems = group
                .Select(party => new AllocationItem
                {
                    Key = party.PartyId,
                    Capacity = party.Capacity,
                    IsDirectPlayerParty = party.IsDirectPlayerParty,
                })
                .ToArray();
            var partyAllocations = AllocateProportionally(partyItems, authorityAllocations[group.Key]);
            foreach (var allocation in partyAllocations)
                result[allocation.Key] = allocation.Value;
        }
    }

    private static Dictionary<string, int> AllocateProportionally(
        IReadOnlyList<AllocationItem> source,
        int requestedSlots)
    {
        var items = source
            .Where(item => item.Capacity > 0)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var result = source.ToDictionary(item => item.Key, _ => 0, StringComparer.Ordinal);
        int totalCapacity = SumCapacity(items);
        int slots = Math.Min(Math.Max(0, requestedSlots), totalCapacity);
        if (slots == 0 || items.Length == 0) return result;

        // A controller's own party gets its minimum before AI parties assigned through army authority. Then give
        // every remaining recipient one when the budget permits and divide the rest by largest remainder.
        foreach (var item in items
            .Where(item => item.IsDirectPlayerParty)
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (slots == 0) break;
            item.Allocated = 1;
            result[item.Key] = 1;
            slots--;
        }

        var withoutMinimum = items.Where(item => item.Allocated == 0).ToArray();
        if (slots >= withoutMinimum.Length)
        {
            foreach (var item in withoutMinimum)
            {
                item.Allocated = 1;
                result[item.Key] = 1;
                slots--;
            }
        }

        if (slots == 0) return result;

        int remainingCapacity = 0;
        foreach (var item in items)
            remainingCapacity += item.Capacity - item.Allocated;

        int allocated = 0;
        foreach (var item in items)
        {
            int capacity = item.Capacity - item.Allocated;
            long numerator = (long)slots * capacity;
            int share = remainingCapacity == 0 ? 0 : (int)(numerator / remainingCapacity);
            item.Remainder = remainingCapacity == 0 ? 0 : numerator % remainingCapacity;
            item.Allocated += share;
            result[item.Key] = item.Allocated;
            allocated += share;
        }

        int leftover = slots - allocated;
        foreach (var item in items
            .Where(item => item.Allocated < item.Capacity)
            .OrderByDescending(item => item.Remainder)
            .ThenByDescending(item => item.IsDirectPlayerParty)
            .ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            if (leftover == 0) break;
            item.Allocated++;
            result[item.Key] = item.Allocated;
            leftover--;
        }

        return result;
    }

    private static void AllocateSideSlots(
        int battleSize,
        int defenderCapacity,
        int attackerCapacity,
        out int defenderSlots,
        out int attackerSlots)
    {
        int totalCapacity = defenderCapacity + attackerCapacity;
        int slots = Math.Min(Math.Max(0, battleSize), totalCapacity);
        if (slots == 0)
        {
            defenderSlots = 0;
            attackerSlots = 0;
            return;
        }

        if (slots == totalCapacity)
        {
            defenderSlots = defenderCapacity;
            attackerSlots = attackerCapacity;
            return;
        }

        float defenderRatio = (float)defenderCapacity / totalCapacity;
        float attackerRatio = (float)attackerCapacity / totalCapacity;
        if (defenderRatio > MaximumBattleSideRatio)
        {
            defenderRatio = MaximumBattleSideRatio;
            attackerRatio = 1f - MaximumBattleSideRatio;
        }
        else if (attackerRatio > MaximumBattleSideRatio)
        {
            attackerRatio = MaximumBattleSideRatio;
            defenderRatio = 1f - MaximumBattleSideRatio;
        }

        bool defenderIsSmaller = defenderRatio < attackerRatio;
        int smallerCapacity = defenderIsSmaller ? defenderCapacity : attackerCapacity;
        float smallerRatio = defenderIsSmaller ? defenderRatio : attackerRatio;
        int smallerSlots = Math.Min(smallerCapacity, (int)Math.Ceiling(smallerRatio * slots));
        int largerSlots = slots - smallerSlots;

        defenderSlots = defenderIsSmaller ? smallerSlots : largerSlots;
        attackerSlots = defenderIsSmaller ? largerSlots : smallerSlots;
    }

    private static int SumCapacity(IEnumerable<BattleInitialSpawnParty> parties)
    {
        int total = 0;
        foreach (var party in parties) total += party.Capacity;
        return total;
    }

    private static int SumCapacity(IEnumerable<AllocationItem> items)
    {
        int total = 0;
        foreach (var item in items) total += item.Capacity;
        return total;
    }
}
