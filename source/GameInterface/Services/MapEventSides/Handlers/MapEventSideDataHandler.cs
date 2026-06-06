using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using JetBrains.Annotations;
using Serilog;
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

    private static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataHandler>();

    public MapEventSideDataHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Subscribe<NetworkChangeMapEventSideIFaction>(Handle);
        messageBroker.Subscribe<MapEventPartyAdded>(Handle);
        messageBroker.Subscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Subscribe<NetworkAddMapEventParty>(Handle);
        messageBroker.Subscribe<NetworkRemoveMapEventParty>(Handle);

        messageBroker.Subscribe<MapEventSideAssigned>(Handle_MapEventSideAssigned);
        messageBroker.Subscribe<NetworkAssignMapEventSide>(Handle_NetworkAssignMapEventSide);

        messageBroker.Subscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
        messageBroker.Subscribe<NetworkAddBattleParty>(Handle_NetworkAddBattleParty);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangeMapEventSideIFaction>(Handle);
        messageBroker.Unsubscribe<MapEventPartyAdded>(Handle);
        messageBroker.Unsubscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkAddMapEventParty>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveMapEventParty>(Handle);
    }

    private void Handle(MessagePayload<MapEventSideIFactionChanged> payload)
    {
        var payloadData = payload.What;
        bool isKingdom = false;

        string factionId;

        if (objectManager.TryGetId(payloadData.Side, out var mapEventSideId) == false) return;
        if (objectManager.TryGetId(payloadData.Faction as Kingdom, out factionId) == false &&
            objectManager.TryGetId(payloadData.Faction as Clan, out factionId) == false) return;

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

    private void Handle(MessagePayload<MapEventPartyRemoved> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetId(data.MapEventSide, out string sideId) == false) return;
        if (objectManager.TryGetId(data.MapEventParty, out string partyId) == false) return;

        network.SendAll(new NetworkRemoveMapEventParty(sideId, partyId));
    }

    private void Handle(MessagePayload<MapEventPartyAdded> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetId(data.MapEventSide, out string sideId) == false) return;
        if (objectManager.TryGetId(data.MapEventParty, out string partyId) == false) return;

        network.SendAll(new NetworkAddMapEventParty(sideId, partyId));
    }

    private void Handle(MessagePayload<NetworkRemoveMapEventParty> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObject<MapEventParty>(data.PartyId, out var party) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MapEventParty), data.PartyId);
            return;
        }
        if (objectManager.TryGetObject<MapEventSide>(data.SideId, out var side) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MapEventSide), data.SideId);
            return;
        }

        using (new AllowedThread())
        {
            side._battleParties.Remove(party);
        }
    }

    private void Handle(MessagePayload<NetworkAddMapEventParty> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObject<MapEventParty>(data.PartyId, out var party) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MapEventParty), data.PartyId);
            return;
        }
        if (objectManager.TryGetObject<MapEventSide>(data.SideId, out var side) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MapEventSide), data.SideId);
            return;
        }

        using (new AllowedThread())
        {
            Logger.Debug("Adding {PartyId} to side {SideId} in map event ({MapEvent})", 
                data.PartyId,
                data.SideId,
                side.MapEvent.StringId ?? "<null>");

            side._battleParties.Add(party);
        }
    }

    private void Handle_MapEventSideAssigned(MessagePayload<MapEventSideAssigned> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEventSide, out var mapEventSideId)) return;

        var message = new NetworkAssignMapEventSide(mapEventId, mapEventSideId, payload.What.Side);
        network.SendAll(message);
    }

    private void Handle_NetworkAssignMapEventSide(MessagePayload<NetworkAssignMapEventSide> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(payload.What.MapEventId, out var mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging<MapEventSide>(payload.What.MapEventSideId, out var mapEventSide)) return;

        using (new AllowedThread())
        {
            mapEvent._sides[(int)payload.What.Side] = mapEventSide;
        }
    }

    private void Handle_MapEventPartyBattlePartyAdded(MessagePayload<MapEventPartyBattlePartyAdded> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEventParty, out var mapEventPartyId))
            return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEventSide, out var mapEventSideId))
            return;

        var message = new NetworkAddBattleParty(mapEventSideId, mapEventPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkAddBattleParty(MessagePayload<NetworkAddBattleParty> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<MapEventSide>(payload.What.MapEventSideId, out var mapEventSide))
            return;
        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(payload.What.MapEventPartyId, out var mapEventParty))
            return;

        using (new AllowedThread())
        {
            mapEventSide._battleParties.Add(mapEventParty);
        }
    }
}
