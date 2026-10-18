using Missions.Agents.Messages;
using System;
using System.Collections.Generic;

namespace Missions.Missiles;

/// <summary>Decides which pickup of a stuck missile item wins, the same way on every client.</summary>
public interface IMissilePickupArbiter
{
    /// <summary>
    /// Offers a pickup that consumed a stuck missile item. False when a better ranked pickup of the same item already
    /// won; otherwise it wins and <paramref name="displaced"/> is the earlier winner it beat, if any.
    /// </summary>
    bool TryClaim(NetworkWeaponPickedup pickup, out NetworkWeaponPickedup displaced);
}

/// <inheritdoc/>
public class MissilePickupArbiter : IMissilePickupArbiter
{
    private const int MaxTrackedItems = 1024;

    private readonly Dictionary<Guid, NetworkWeaponPickedup> winners = new();
    private readonly Queue<Guid> winnerOrder = new();

    // Every client sees every pickup of the item, so ranking by pickup id picks the same winner everywhere without
    // a round trip, whatever order the pickups arrive in.
    public bool TryClaim(NetworkWeaponPickedup pickup, out NetworkWeaponPickedup displaced)
    {
        displaced = null;
        if (pickup == null || pickup.WorldItemId == Guid.Empty) return true;

        if (!winners.TryGetValue(pickup.WorldItemId, out NetworkWeaponPickedup winner))
        {
            Track(pickup);
            return true;
        }

        if (winner.PickupId == pickup.PickupId) return true;
        if (winner.PickupId.CompareTo(pickup.PickupId) < 0) return false;

        displaced = winner;
        winners[pickup.WorldItemId] = pickup;
        return true;
    }

    // Keeps the item's winner, forgetting the oldest item once the cap is reached since contested pickups land close together.
    private void Track(NetworkWeaponPickedup pickup)
    {
        winners.Add(pickup.WorldItemId, pickup);
        winnerOrder.Enqueue(pickup.WorldItemId);
        while (winnerOrder.Count > MaxTrackedItems)
            winners.Remove(winnerOrder.Dequeue());
    }
}
