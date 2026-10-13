using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Ships.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Handlers;

internal class LootShipsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public LootShipsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<LootShipsDiscardRequested>(Handle_LootShipsDiscardRequested);
        messageBroker.Subscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LootShipsDiscardRequested>(Handle_LootShipsDiscardRequested);
        messageBroker.Unsubscribe<NetworkDiscardLootShips>(Handle_NetworkDiscardLootShips);
    }

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

    private void Handle_NetworkDiscardLootShips(MessagePayload<NetworkDiscardLootShips> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyHandle, out var party)) return;

            if (data.LootShipHandles == null) return;
            foreach (var shipHandle in data.LootShipHandles)
            {
                if (!objectManager.TryGetObjectWithLogging<Ship>(shipHandle, out var ship)) continue;

                // Matches (ship.Owner != PartyBase.MainParty) but for multiple players
                if (ship.Owner == party.Party || ship.Owner.IsPlayerParty()) continue;

                DestroyShipAction.Apply(ship);
            }
        });
    }
}
