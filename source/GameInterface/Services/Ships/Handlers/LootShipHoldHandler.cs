using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Ships.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Server] Releases a looter's held loot ships when its loot screen's discard arrives, and sweeps the hold about once a second
/// on the campaign tick, which runs while time is paused too.
/// </summary>
internal class LootShipHoldHandler : IHandler
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ILootShipHold lootShipHold;
    private DateTime nextSweepUtc;

    public LootShipHoldHandler(IMessageBroker messageBroker, IObjectManager objectManager, ILootShipHold lootShipHold)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.lootShipHold = lootShipHold;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

    // Published from the Campaign.Tick postfix, so RunSafe runs the sweep inline on the game thread.
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;

        var now = DateTime.UtcNow;
        if (now < nextSweepUtc) return;

        nextSweepUtc = now + SweepInterval;
        GameThread.RunSafe(lootShipHold.Sweep, context: nameof(Handle_CampaignTick));
    }

    // Sent by the looter's OnPlayerLootShipEnd when its ship loot screen closes; LootShipsHandler destroys the listed ships it
    // left, so a ship it already destroyed doesn't resolve here, and Release finds it gone and only forgets it.
    private void Handle_NetworkDiscardLootShips(MessagePayload<NetworkDiscardLootShips> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject<MobileParty>(data.PartyHandle, out var looter)) return;

            var offered = new List<Ship>();
            foreach (var shipHandle in data.LootShipHandles ?? Array.Empty<uint>())
            {
                if (objectManager.TryGetObject<Ship>(shipHandle, out var ship)) offered.Add(ship);
            }

            lootShipHold.Release(looter.Party, offered);
        }, context: nameof(Handle_NetworkDiscardLootShips));
    }
}
