using Common;
using Common.Messaging;
using GameInterface.Registry.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Ships.Messages;
using System;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Server] Sweeps the loot ship hold on the campaign tick, which runs while time is paused too; LootShipsHandler releases it.
/// [Joining client] Takes the server's held loot ships back out of their looter's list once the save's objects are registered.
/// </summary>
internal class LootShipHoldHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ILootShipHold lootShipHold;
    private volatile string[] heldLootShipIds = Array.Empty<string>();

    public LootShipHoldHandler(IMessageBroker messageBroker, ILootShipHold lootShipHold)
    {
        this.messageBroker = messageBroker;
        this.lootShipHold = lootShipHold;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<InitializeClientHeldLootShips>(Handle_InitializeClientHeldLootShips);
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<InitializeClientHeldLootShips>(Handle_InitializeClientHeldLootShips);
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(lootShipHold.Sweep, context: nameof(Handle_CampaignTick));
    }

    // Arrives with the join save, before the campaign loads and registers its objects.
    private void Handle_InitializeClientHeldLootShips(MessagePayload<InitializeClientHeldLootShips> payload)
    {
        heldLootShipIds = payload.What.HeldLootShipIds ?? Array.Empty<string>();
    }

    // The save carried the held loot ships in their looter's list so they register under the server's ids; take them out again
    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (heldLootShipIds.Length == 0) return;

        var shipIds = heldLootShipIds;
        heldLootShipIds = Array.Empty<string>();
        GameThread.RunSafe(() => lootShipHold.DetachFromJoinSnapshot(shipIds), context: nameof(Handle_AllGameObjectsRegistered));
    }
}
