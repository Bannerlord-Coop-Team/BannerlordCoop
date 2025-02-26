using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventSides.Handlers;
internal class MapEventSideDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public MapEventSideDataHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Subscribe<NetworkChangeMapEventSideIFaction>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangeMapEventSideIFaction>(Handle);
    }

    private void Handle(MessagePayload<MapEventSideIFactionChanged> payload)
    {
        var payloadData = payload.What;
        bool isKingdom = false;

        if (objectManager.TryGetId(payloadData.Side, out var mapEventSideId) == false) return;
        if (objectManager.TryGetId(payloadData.Faction, out var factionId) == false) return;

        if(payloadData.Faction.GetType().Equals(typeof(Kingdom)))
        {
            isKingdom = true;
        }

        var message = new NetworkChangeMapEventSideIFaction(mapEventSideId, factionId, isKingdom);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkChangeMapEventSideIFaction> payload)
    {
        var payloadData = payload.What;

        if (objectManager.TryGetObject<MapEventSide>(payloadData.SideId, out var mapEventSide) == false) return;

        if (payloadData.IsKingdom)
        {
            if (objectManager.TryGetObject(payloadData.FactionId, out Kingdom kingdom) == false) return;
            UpdateIFaction(mapEventSide, kingdom);
        }
        else
        {
            if (objectManager.TryGetObject(payloadData.FactionId, out Clan clan) == false) return;
            UpdateIFaction(mapEventSide, clan);
        }
    }

    private void UpdateIFaction(MapEventSide side, IFaction faction)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                side._mapFaction = faction;
            }
        });
    }
}
