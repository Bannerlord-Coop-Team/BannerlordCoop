using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
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
    private readonly IMapEventInitializationBarrier initializationBarrier;
    private readonly IEncounterMenuConditionRefresher encounterMenuConditionRefresher;

    private static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataHandler>();

    public MapEventSideDataHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventInitializationBarrier initializationBarrier,
        IEncounterMenuConditionRefresher encounterMenuConditionRefresher)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.initializationBarrier = initializationBarrier;
        this.encounterMenuConditionRefresher = encounterMenuConditionRefresher;

        messageBroker.Subscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveMapEventParty>(Handle);

        messageBroker.Subscribe<MapEventSideAssigned>(Handle_MapEventSideAssigned);
        messageBroker.Subscribe<NetworkAssignMapEventSide>(Handle_NetworkAssignMapEventSide);

        messageBroker.Subscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
        messageBroker.Subscribe<NetworkAddBattleParty>(Handle_NetworkAddBattleParty);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventPartyRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveMapEventParty>(Handle);
        messageBroker.Unsubscribe<MapEventSideAssigned>(Handle_MapEventSideAssigned);
        messageBroker.Unsubscribe<NetworkAssignMapEventSide>(Handle_NetworkAssignMapEventSide);
        messageBroker.Unsubscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
        messageBroker.Unsubscribe<NetworkAddBattleParty>(Handle_NetworkAddBattleParty);
    }

    private void Handle(MessagePayload<MapEventPartyRemoved> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetId(data.MapEventSide, out string sideId) == false) return;
        if (objectManager.TryGetId(data.MapEventParty, out string partyId) == false) return;

        network.SendAll(new NetworkRemoveMapEventParty(sideId, partyId));
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
                    if (party.Party?.MapEventSide == side) party.Party._mapEventSide = null;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkRemoveMapEventParty");
            }
        });
    }

    private void Handle_MapEventSideAssigned(MessagePayload<MapEventSideAssigned> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEventSide, out var mapEventSideId)) return;

        Logger.Information(
            "[SideDiag][server] assign side {Side} of {MapEventId} -> {SideId} (MissionSide={MissionSide}, leader={Leader})",
            payload.What.Side, mapEventId, mapEventSideId,
            payload.What.MapEventSide?.MissionSide, payload.What.MapEventSide?.LeaderParty?.Id ?? "<none>");

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

                // A mismatch here means every party added to this side afterwards lands on the wrong one.
                if ((int)mapEventSide.MissionSide != side)
                {
                    Logger.Error(
                        "[SideDiag][client] MISMATCH: assigned index {Index} but the side reports MissionSide={MissionSide} ({SideId})",
                        side, mapEventSide.MissionSide, data.MapEventSideId);
                }
                else
                {
                    Logger.Information(
                        "[SideDiag][client] assigned index {Index} <- {SideId} (MissionSide={MissionSide})",
                        side, data.MapEventSideId, mapEventSide.MissionSide);
                }
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

        Logger.Information(
            "[SideDiag][server] add party {Party} -> side {SideId} (MissionSide={MissionSide}, leader={Leader})",
            payload.What.MapEventParty?.Party?.Id ?? mapEventPartyId, mapEventSideId,
            payload.What.MapEventSide?.MissionSide, payload.What.MapEventSide?.LeaderParty?.Id ?? "<none>");

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

                var addedParty = mapEventParty.Party;
                var isLocalPlayer = addedParty != null && ReferenceEquals(addedParty, PartyBase.MainParty);

                initializationBarrier.AttachClient(
                    mapEventSide,
                    mapEventParty,
                    () => AfterClientPartyAttached(mapEventSide.MapEvent));

                // Only the local player's own placement is worth a line: that is the one whose disagreement
                // with the server makes the spawn handler look for its troops in the other side's reserve
                // and abort the battle.
                if (isLocalPlayer)
                {
                    Logger.Information(
                        "[SideDiag][client] LOCAL PLAYER {Party} attached to side {SideId} (MissionSide={MissionSide}, leader={Leader}); MainParty.Side now {Resolved}",
                        addedParty.Id, data.MapEventSideId, mapEventSide.MissionSide,
                        mapEventSide.LeaderParty?.Id ?? "<none>", PartyBase.MainParty.Side);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply NetworkAddBattleParty");
            }
        });
    }

    private void AfterClientPartyAttached(MapEvent mapEvent)
    {
        if (ModInformation.IsServer)
            return;

        SwitchRaiderToEncounterIfNeeded(mapEvent);
        encounterMenuConditionRefresher.RefreshForMapEvent(mapEvent);
    }

    private static void SwitchRaiderToEncounterIfNeeded(MapEvent mapEvent)
    {
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
