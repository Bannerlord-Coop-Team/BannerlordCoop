using Common.Messaging;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Carries the ids of the loot ships the server held ownerless to a joining client as part of the save transfer
/// </summary>
public readonly struct InitializeClientHeldLootShips : IEvent
{
    public readonly string[] HeldLootShipIds;

    public InitializeClientHeldLootShips(string[] heldLootShipIds)
    {
        HeldLootShipIds = heldLootShipIds;
    }
}
