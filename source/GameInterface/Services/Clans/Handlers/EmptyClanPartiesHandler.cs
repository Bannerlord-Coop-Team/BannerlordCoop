using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Clans.Handlers;

internal class EmptyClanPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IEmptyClanPartiesCampaignBehaviorInterface emptyClanPartiesInterface;

    public EmptyClanPartiesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IEmptyClanPartiesCampaignBehaviorInterface emptyClanPartiesInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.emptyClanPartiesInterface = emptyClanPartiesInterface;

        messageBroker.Subscribe<EmptyClanPartyAdded>(Handle_EmptyClanPartyAdded);
        messageBroker.Subscribe<NetworkEmptyClanPartyAdded>(Handle_NetworkEmptyClanPartyAdded);
        messageBroker.Subscribe<EmptyClanPartyRemoved>(Handle_EmptyClanPartyRemoved);
        messageBroker.Subscribe<NetworkEmptyClanPartyRemoved>(Handle_NetworkEmptyClanPartyRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EmptyClanPartyAdded>(Handle_EmptyClanPartyAdded);
        messageBroker.Unsubscribe<NetworkEmptyClanPartyAdded>(Handle_NetworkEmptyClanPartyAdded);
        messageBroker.Unsubscribe<EmptyClanPartyRemoved>(Handle_EmptyClanPartyRemoved);
        messageBroker.Unsubscribe<NetworkEmptyClanPartyRemoved>(Handle_NetworkEmptyClanPartyRemoved);
    }

    private void Handle_EmptyClanPartyAdded(MessagePayload<EmptyClanPartyAdded> obj)
    {
        var data = obj.What;
        if (!objectManager.TryGetIdWithLogging(data.Leader, out var leaderId)) return;

        var shipIds = new List<string>();
        foreach (var ship in data.Ships)
        {
            if (objectManager.TryGetIdWithLogging(ship, out var shipId)) shipIds.Add(shipId);
        }

        network.SendAll(new NetworkEmptyClanPartyAdded(leaderId, shipIds.ToArray()));
        messageBroker.Publish(this, new ClanManagementChanged(data.Leader.Clan, ClanManagementRefresh.Parties));
    }

    private void Handle_NetworkEmptyClanPartyAdded(MessagePayload<NetworkEmptyClanPartyAdded> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.LeaderId, out var leader)) return;

            var ships = new List<Ship>();
            foreach (var shipId in data.ShipIds ?? Array.Empty<string>())
            {
                if (objectManager.TryGetObjectWithLogging<Ship>(shipId, out var ship)) ships.Add(ship);
            }

            emptyClanPartiesInterface.AddEmptyClanParty(leader, ships);
        });
    }

    private void Handle_EmptyClanPartyRemoved(MessagePayload<EmptyClanPartyRemoved> obj)
    {
        var data = obj.What;
        if (!objectManager.TryGetIdWithLogging(data.Leader, out var leaderId)) return;

        network.SendAll(new NetworkEmptyClanPartyRemoved(leaderId));
        messageBroker.Publish(this, new ClanManagementChanged(data.Leader.Clan, ClanManagementRefresh.Parties));
    }

    private void Handle_NetworkEmptyClanPartyRemoved(MessagePayload<NetworkEmptyClanPartyRemoved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.LeaderId, out var leader)) return;

            emptyClanPartiesInterface.RemoveEmptyClanParty(leader);
        });
    }
}
