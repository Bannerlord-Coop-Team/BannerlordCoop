using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventSides.Handlers;
internal class MapEventSideDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventBattleSizeCorrection mapEventBattleSizeCorrection;

    private static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataHandler>();

    public MapEventSideDataHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventBattleSizeCorrection mapEventBattleSizeCorrection)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventBattleSizeCorrection = mapEventBattleSizeCorrection;

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
        GameThread.RunSafe(() =>
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

        GameThread.RunSafe(() =>
        {
            try
            {
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
                    if (party.Party?.MapEventSide == side)
                        party.Party._mapEventSide = null;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkRemoveMapEventParty");
            }
        });
    }

    private void Handle(MessagePayload<NetworkAddMapEventParty> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            try
            {
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

                    party.Party._mapEventSide = side;
                    side._battleParties.Add(party);
                }

                mapEventBattleSizeCorrection.TryCorrect(side.MapEvent);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkAddMapEventParty");
            }
        });
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
        var data = payload.What;

        var side = (int)data.Side;

        GameThread.RunSafe(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
                if (!objectManager.TryGetObjectWithLogging<MapEventSide>(data.MapEventSideId, out var mapEventSide)) return;

                using (new AllowedThread())
                {
                    mapEvent._sides[side] = mapEventSide;
                }

                mapEventBattleSizeCorrection.TryCorrect(mapEvent);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkAssignMapEventSide");
            }
        });
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
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventSide>(data.MapEventSideId, out var mapEventSide))
                    return;
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(data.MapEventPartyId, out var mapEventParty))
                    return;

                using (new AllowedThread())
                {
                    mapEventParty.Party._mapEventSide = mapEventSide;
                    mapEventSide._battleParties.Add(mapEventParty);
                }

                mapEventBattleSizeCorrection.TryCorrect(mapEventSide.MapEvent);
                SwitchRaiderToEncounterIfNeeded(mapEventSide.MapEvent);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkAddBattleParty");
            }
        });
    }

    private static void SwitchRaiderToEncounterIfNeeded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer)
            return;

        if (mapEvent.IsRaidHostileAction() == false || mapEvent.IsActiveSlowVillageRaid())
            return;

        if (MobileParty.MainParty?.MapEvent != mapEvent)
            return;

        if (PlayerEncounter.Current == null)
            return;

        var encounterMapEvent = PlayerEncounter.Battle ?? PlayerEncounter.EncounteredBattle ?? MapEvent.PlayerMapEvent;
        if (encounterMapEvent != mapEvent)
            return;

        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "raiding_village")
            return;

        GameMenu.SwitchToMenu("encounter");
    }
}
