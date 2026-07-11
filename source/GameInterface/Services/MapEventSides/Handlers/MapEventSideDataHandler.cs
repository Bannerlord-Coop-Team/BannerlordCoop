using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Initialization;
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
    private readonly IMapEventInitializationTracker mapEventInitializationTracker;

    private static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataHandler>();

    public MapEventSideDataHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventBattleSizeCorrection mapEventBattleSizeCorrection,
        IMapEventInitializationTracker mapEventInitializationTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventBattleSizeCorrection = mapEventBattleSizeCorrection;
        this.mapEventInitializationTracker = mapEventInitializationTracker;

        messageBroker.Subscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Subscribe<NetworkChangeMapEventSideIFaction>(Handle);
        messageBroker.Subscribe<MapEventPartyAdded>(Handle);
        messageBroker.Subscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveMapEventParty>(Handle);

        messageBroker.Subscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
        messageBroker.Subscribe<NetworkAddBattleParty>(Handle_NetworkAddBattleParty);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSideIFactionChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangeMapEventSideIFaction>(Handle);
        messageBroker.Unsubscribe<MapEventPartyAdded>(Handle);
        messageBroker.Unsubscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveMapEventParty>(Handle);
        messageBroker.Unsubscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
        messageBroker.Unsubscribe<NetworkAddBattleParty>(Handle_NetworkAddBattleParty);
    }

    private void Handle(MessagePayload<MapEventSideIFactionChanged> payload)
    {
        var payloadData = payload.What;
        if (IsPendingInitialization(payloadData.Side)) return;

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

        GameThread.RunSafe(() =>
        {
            try
            {
                if (objectManager.TryGetObject<MapEventSide>(payloadData.SideId, out var mapEventSide) == false) return;

                IFaction faction;
                if (payloadData.IsKingdom)
                {
                    if (objectManager.TryGetObject(payloadData.FactionId, out Kingdom kingdom) == false) return;
                    faction = kingdom;
                }
                else
                {
                    if (objectManager.TryGetObject(payloadData.FactionId, out Clan clan) == false) return;
                    faction = clan;
                }

                using (new AllowedThread())
                {
                    mapEventSide._mapFaction = faction;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkChangeMapEventSideIFaction");
            }
        });
    }

    private void Handle(MessagePayload<MapEventPartyRemoved> payload)
    {
        var data = payload.What;
        if (IsPendingInitialization(data.MapEventSide, data.MapEventParty)) return;

        if (objectManager.TryGetId(data.MapEventSide, out string sideId) == false) return;
        if (objectManager.TryGetId(data.MapEventParty, out string partyId) == false) return;

        network.SendAll(new NetworkRemoveMapEventParty(sideId, partyId));
    }

    private void Handle(MessagePayload<MapEventPartyAdded> payload)
    {
        SendBattlePartyAdded(payload.What.MapEventSide, payload.What.MapEventParty);
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

    private void Handle_MapEventPartyBattlePartyAdded(MessagePayload<MapEventPartyBattlePartyAdded> payload)
    {
        SendBattlePartyAdded(payload.What.MapEventSide, payload.What.MapEventParty);
    }

    private void SendBattlePartyAdded(MapEventSide mapEventSide, MapEventParty mapEventParty)
    {
        if (IsPendingInitialization(mapEventSide, mapEventParty))
            return;

        if (!objectManager.TryGetIdWithLogging(mapEventParty, out var mapEventPartyId))
            return;
        if (!objectManager.TryGetIdWithLogging(mapEventSide, out var mapEventSideId))
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

                // Network-created reinforcement objects bypass constructor ownership tracking. Adopt
                // the complete party subgraph as soon as it becomes reachable from the committed root.
                mapEventInitializationTracker.ExtendCommittedGraph(
                    mapEventSide.MapEvent,
                    MapEventGraph.EnumerateParty(mapEventParty));

                mapEventBattleSizeCorrection.TryCorrect(mapEventSide.MapEvent);
                SwitchRaiderToEncounterIfNeeded(mapEventSide.MapEvent);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkAddBattleParty");
            }
        });
    }

    private bool IsPendingInitialization(MapEventSide mapEventSide, MapEventParty mapEventParty = null)
    {
        var mapEvent = mapEventSide?.MapEvent;
        return mapEventInitializationTracker.IsPending(mapEventSide)
            || mapEventInitializationTracker.IsPending(mapEventParty)
            || mapEventInitializationTracker.IsPending(mapEvent)
            || mapEventInitializationTracker.IsBuilding(mapEvent);
    }

    private static void SwitchRaiderToEncounterIfNeeded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer)
            return;

        if (!mapEvent.IsRaidHostileAction() || mapEvent.IsActiveSlowVillageRaid())
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
