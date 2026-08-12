using System;
using System.Collections.Generic;
using Common.Logging;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

internal enum BattleLootClaimStatus
{
    NoGrant,
    Accepted,
    Rejected
}

internal sealed class BattleLootClaim
{
    internal readonly string ControllerId;
    internal readonly long Generation;
    internal readonly long Token;
    internal readonly ItemRosterElement[] DiscardedItems;
    internal readonly ItemRosterElement[] ReturnedOwnedItems;

    internal BattleLootClaim(
        string controllerId,
        long generation,
        long token,
        ItemRosterElement[] discardedItems,
        ItemRosterElement[] returnedOwnedItems)
    {
        ControllerId = controllerId;
        Generation = generation;
        Token = token;
        DiscardedItems = discardedItems;
        ReturnedOwnedItems = returnedOwnedItems;
    }
}

internal interface IBattleLootGrantRegistry
{
    void Stage(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        string mapEventId,
        IEnumerable<ItemRosterElement> items);

    void Forfeit(string controllerId);

    BattleLootClaimStatus TryBeginClaim(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        IEnumerable<ItemRosterElement> requestedItems,
        IEnumerable<ItemRosterElement> remainingItems,
        out BattleLootClaim claim,
        out string reason);

    bool Consume(BattleLootClaim claim);

    void Release(BattleLootClaim claim);
}

/// <summary>
/// Session-scoped authority for the temporary item roster shown after a battle. The loot roster has no
/// object-manager id, so the normal external-roster trade validation cannot authenticate it. This registry
/// captures the server-authored award before it is sent to the winning client and permits one atomic claim
/// containing only a subset of that exact award.
/// </summary>
internal sealed class BattleLootGrantRegistry : IBattleLootGrantRegistry
{
    private const int TombstoneLimitPerController = 64;
    private static readonly ILogger Logger = LogManager.GetLogger<BattleLootGrantRegistry>();

    private sealed class Grant
    {
        internal readonly string OwnerHeroId;
        internal readonly string OwnerPartyId;
        internal readonly string MapEventId;
        internal readonly long Generation;
        internal readonly Dictionary<EquipmentElement, long> Items;
        internal long ActiveToken;

        internal Grant(
            string ownerHeroId,
            string ownerPartyId,
            string mapEventId,
            long generation,
            Dictionary<EquipmentElement, long> items)
        {
            OwnerHeroId = ownerHeroId;
            OwnerPartyId = ownerPartyId;
            MapEventId = mapEventId;
            Generation = generation;
            Items = items;
        }
    }

    private sealed class Tombstones
    {
        internal readonly Queue<string> Order = new();
        internal readonly HashSet<string> MapEventIds = new(StringComparer.Ordinal);
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Grant> grants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tombstones> tombstones = new(StringComparer.Ordinal);
    private long nextGeneration;
    private long nextToken;

    public void Stage(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        string mapEventId,
        IEnumerable<ItemRosterElement> items)
    {
        if (string.IsNullOrEmpty(controllerId) ||
            string.IsNullOrEmpty(ownerHeroId) ||
            string.IsNullOrEmpty(ownerPartyId) ||
            string.IsNullOrEmpty(mapEventId))
        {
            return;
        }

        lock (gate)
        {
            if (IsSettledLocked(controllerId, mapEventId))
                return;

            if (grants.TryGetValue(controllerId, out Grant existing))
            {
                if (string.Equals(existing.MapEventId, mapEventId, StringComparison.Ordinal))
                {
                    // Result publication can be retried. Preserve the first immutable grant instead of
                    // refreshing it or replacing an in-flight claim.
                    return;
                }
            }

            Dictionary<EquipmentElement, long> counts = CountItems(items, out bool valid);
            if (!valid || counts.Count == 0)
            {
                ForfeitLocked(controllerId);
                RememberSettledLocked(controllerId, mapEventId);
                return;
            }

            if (existing != null)
            {
                Logger.Warning(
                    "Forfeiting unclaimed battle loot for controller {ControllerId}, map event {OldMapEventId}, " +
                    "because a newer grant for {NewMapEventId} arrived",
                    controllerId,
                    existing.MapEventId,
                    mapEventId);
                ForfeitLocked(controllerId);
            }

            grants[controllerId] = new Grant(
                ownerHeroId,
                ownerPartyId,
                mapEventId,
                ++nextGeneration,
                counts);
        }
    }

    public void Forfeit(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
            return;

        lock (gate)
            ForfeitLocked(controllerId);
    }

    public BattleLootClaimStatus TryBeginClaim(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        IEnumerable<ItemRosterElement> requestedItems,
        IEnumerable<ItemRosterElement> remainingItems,
        out BattleLootClaim claim,
        out string reason)
    {
        claim = null;
        reason = null;

        lock (gate)
        {
            if (string.IsNullOrEmpty(controllerId) ||
                !grants.TryGetValue(controllerId, out Grant grant) ||
                !string.Equals(grant.OwnerHeroId, ownerHeroId, StringComparison.Ordinal) ||
                !string.Equals(grant.OwnerPartyId, ownerPartyId, StringComparison.Ordinal))
            {
                return BattleLootClaimStatus.NoGrant;
            }

            if (grant.ActiveToken != 0)
            {
                reason = "Your previous battle-loot claim is still being processed.";
                return BattleLootClaimStatus.Rejected;
            }

            Dictionary<EquipmentElement, long> requested =
                CountItems(requestedItems, out bool valid);
            if (!valid)
            {
                reason = "The submitted battle loot contained invalid item counts.";
                return BattleLootClaimStatus.Rejected;
            }

            // The left-hand loot roster is a temporary client-only InventoryLogic roster.
            // Bannerlord can rebuild or dispose it before DoneLogic is serialized, so it is
            // not an authoritative snapshot of the award. Parse it only to reject malformed
            // state; ownership is proven by the immutable server grant and the requested
            // bought-item subset below.
            CountItems(remainingItems, out valid);
            if (!valid)
            {
                reason = "The submitted battle-loot roster contained invalid item counts.";
                return BattleLootClaimStatus.Rejected;
            }

            foreach (KeyValuePair<EquipmentElement, long> item in requested)
            {
                if (!grant.Items.TryGetValue(item.Key, out long available) ||
                    item.Value > available)
                {
                    reason = "The selected items were not present in your server-awarded battle loot.";
                    return BattleLootClaimStatus.Rejected;
                }
            }

            var discardedAward = new Dictionary<EquipmentElement, long>();
            foreach (KeyValuePair<EquipmentElement, long> item in grant.Items)
            {
                requested.TryGetValue(item.Key, out long taken);
                long notTaken = item.Value - taken;
                if (notTaken > 0)
                    discardedAward[item.Key] = notTaken;
            }

            if (!TryCreateRosterElements(
                    discardedAward, out ItemRosterElement[] discarded))
            {
                reason = "The submitted battle-loot roster was too large to validate safely.";
                return BattleLootClaimStatus.Rejected;
            }

            long token = ++nextToken;
            grant.ActiveToken = token;
            claim = new BattleLootClaim(
                controllerId,
                grant.Generation,
                token,
                discarded,
                Array.Empty<ItemRosterElement>());
            return BattleLootClaimStatus.Accepted;
        }
    }

    public bool Consume(BattleLootClaim claim)
    {
        if (claim == null)
            return true;

        lock (gate)
        {
            if (!TryGetMatchingGrant(claim, out _))
                return false;

            Grant grant = grants[claim.ControllerId];
            RememberSettledLocked(claim.ControllerId, grant.MapEventId);
            grants.Remove(claim.ControllerId);
            return true;
        }
    }

    public void Release(BattleLootClaim claim)
    {
        if (claim == null)
            return;

        lock (gate)
        {
            if (TryGetMatchingGrant(claim, out Grant grant))
                grant.ActiveToken = 0;
        }
    }

    private bool TryGetMatchingGrant(BattleLootClaim claim, out Grant grant)
    {
        return grants.TryGetValue(claim.ControllerId, out grant) &&
            grant.Generation == claim.Generation &&
            grant.ActiveToken == claim.Token;
    }

    private void ForfeitLocked(string controllerId)
    {
        if (!grants.TryGetValue(controllerId, out Grant grant))
            return;

        RememberSettledLocked(controllerId, grant.MapEventId);
        grants.Remove(controllerId);
    }

    private bool IsSettledLocked(string controllerId, string mapEventId) =>
        tombstones.TryGetValue(controllerId, out Tombstones settled) &&
        settled.MapEventIds.Contains(mapEventId);

    private void RememberSettledLocked(string controllerId, string mapEventId)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(mapEventId))
            return;

        if (!tombstones.TryGetValue(controllerId, out Tombstones settled))
        {
            settled = new Tombstones();
            tombstones.Add(controllerId, settled);
        }

        if (!settled.MapEventIds.Add(mapEventId))
            return;

        settled.Order.Enqueue(mapEventId);
        while (settled.Order.Count > TombstoneLimitPerController)
            settled.MapEventIds.Remove(settled.Order.Dequeue());
    }

    private static Dictionary<EquipmentElement, long> CountItems(
        IEnumerable<ItemRosterElement> items,
        out bool valid)
    {
        valid = true;
        var result = new Dictionary<EquipmentElement, long>();
        if (items == null)
            return result;

        foreach (ItemRosterElement item in items)
        {
            if (item.EquipmentElement.Item == null)
            {
                // ItemRoster._data may contain default backing-capacity rows. They carry no state and
                // must not invalidate an otherwise authoritative snapshot.
                if (item.Amount == 0)
                    continue;

                valid = false;
                return result;
            }
            if (item.Amount <= 0)
            {
                valid = false;
                return result;
            }

            result.TryGetValue(item.EquipmentElement, out long current);
            long next;
            try
            {
                next = checked(current + item.Amount);
            }
            catch (OverflowException)
            {
                valid = false;
                return result;
            }
            result[item.EquipmentElement] = next;
        }

        return result;
    }

    private static bool TryCreateRosterElements(
        IEnumerable<KeyValuePair<EquipmentElement, long>> counts,
        out ItemRosterElement[] elements)
    {
        var result = new List<ItemRosterElement>();
        foreach (KeyValuePair<EquipmentElement, long> item in counts)
        {
            if (item.Value <= 0 || item.Value > int.MaxValue)
            {
                elements = null;
                return false;
            }
            result.Add(new ItemRosterElement(
                item.Key,
                (int)item.Value));
        }
        elements = result.ToArray();
        return true;
    }
}
