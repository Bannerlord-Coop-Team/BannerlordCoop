using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Ships.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// Discards the loot ships a player's ship loot screen left: the client sends the discard, the server destroys them and
/// releases the player's loot ship hold.
/// </summary>
internal class LootShipsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILootShipHold lootShipHold;

    // Takes the broker it subscribes on, the object manager for the handles, the network the discard goes over and the hold.
    public LootShipsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILootShipHold lootShipHold)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.lootShipHold = lootShipHold;

        messageBroker.Subscribe<LootShipsDiscardRequested>(Handle_LootShipsDiscardRequested);
        messageBroker.Subscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

    // Drops the handler's subscriptions.
    public void Dispose()
    {
        messageBroker.Unsubscribe<LootShipsDiscardRequested>(Handle_LootShipsDiscardRequested);
        messageBroker.Unsubscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

    // [Client] Sends the ships the closing loot screen offered to the server.
    private void Handle_LootShipsDiscardRequested(MessagePayload<LootShipsDiscardRequested> obj)
    {
        if (!objectManager.TryGetHandleWithLogging(obj.What.Party, out var partyHandle)) return;

        var lootShipHandles = new List<uint>();
        foreach (var ship in obj.What.LootShips)
        {
            if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) continue;
            
            lootShipHandles.Add(shipHandle);
        }

        network.SendAll(new NetworkDiscardLootShips(partyHandle, lootShipHandles.ToArray()));
    }

    // [Server] Destroys the offered ships no player took, then releases the looter's hold, which destroys any it didn't list.
    private void Handle_NetworkDiscardLootShips(MessagePayload<NetworkDiscardLootShips> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyHandle, out var party)) return;

            foreach (var shipHandle in data.LootShipHandles ?? Array.Empty<uint>())
            {
                if (!objectManager.TryGetObjectWithLogging<Ship>(shipHandle, out var ship)) continue;

                // Matches (ship.Owner != PartyBase.MainParty) but for multiple players
                if (ship.Owner == party.Party || ship.Owner.IsPlayerParty()) continue;

                DestroyShipAction.Apply(ship);
            }

            lootShipHold.Release(party.Party);
        }, context: nameof(Handle_NetworkDiscardLootShips));
    }
}
