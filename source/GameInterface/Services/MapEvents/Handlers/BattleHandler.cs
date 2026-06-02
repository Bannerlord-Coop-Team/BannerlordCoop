using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Handlers;

internal class BattleHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IPlayerRegistry playerRegistry;
    private readonly ITimeControlInterface timeControlInterface;

    public BattleHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IPlayerRegistry playerRegistry,
        ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.playerRegistry = playerRegistry;
        this.timeControlInterface = timeControlInterface;
        this.mapEventLogger = mapEventLogger;
        messageBroker.Subscribe<BattleStarted>(Handle_BattleStarted);
        messageBroker.Subscribe<NetworkStartBattle>(Handle_NetworkStartBattle);

        messageBroker.Subscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);

        messageBroker.Subscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Subscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);

        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);

        messageBroker.Subscribe<AttackMissionAttempted>(Handle_AttackMissionAttempted);
        messageBroker.Subscribe<NetworkAttackMissionAttempted>(Handle_NetworkAttackMissionAttempted);
        messageBroker.Subscribe<NetworkStartAttackMission>(Handle_NetworkStartAttackMission);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleStarted>(Handle_BattleStarted);
        messageBroker.Unsubscribe<NetworkStartBattle>(Handle_NetworkStartBattle);

        messageBroker.Unsubscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Unsubscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);

        messageBroker.Unsubscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
    }

    private void Handle_AttackMissionAttempted(MessagePayload<AttackMissionAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId))
            return;

        mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Handling attack mission attempted for map event");

        var message = new NetworkAttackMissionAttempted(mapEventId);
        network.SendAll(message);
    }

    private void Handle_NetworkAttackMissionAttempted(MessagePayload<NetworkAttackMissionAttempted> payload)
    {
        if (!objectManager.TryGetObject(payload.What.MapEventId, out MapEvent mapEvent))
            return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network attack mission attempted for map event. Setting attack mission attempted to true");

        foreach(var side in mapEvent._sides)
        {
            side.MakeReadyForMission(null);
        }

        var message = new NetworkStartAttackMission();
        network.SendAll(message);
    }

    private void Handle_NetworkStartAttackMission(MessagePayload<NetworkStartAttackMission> payload)
    {
        var battle = PlayerEncounter.Battle;
        bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
        CampaignVec2 position = MobileParty.MainParty.Position;

        IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
        MapPatchData mapPatchAtPosition = mapSceneWrapper.GetMapPatchAtPosition(position);


        string battleScene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
        MissionInitializerRecord rec2 = new MissionInitializerRecord(battleScene);
        TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
        rec2.TerrainType = (int)faceTerrainType2;
        rec2.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        rec2.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        rec2.NeedsRandomTerrain = false;
        rec2.PlayingInCampaignMode = true;

        // TODO make this server side
        rec2.RandomTerrainSeed = MBRandom.RandomInt(10000);
        rec2.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position);
        rec2.SceneHasMapPatch = true;
        rec2.DecalAtlasGroup = 2;
        rec2.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
        position = battle.AttackerSide.LeaderParty.Position;
        Vec2 v2 = position.ToVec2();
        position = battle.DefenderSide.LeaderParty.Position;
        rec2.PatchEncounterDir = (v2 - position.ToVec2()).Normalized();

        CampaignMission.OpenBattleMission(rec2);
    }

    private void Handle_BattleStarted(MessagePayload<BattleStarted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Attacker, out var attackerPartyBaseId))
            return;

        if (!objectManager.TryGetIdWithLogging(data.Defender, out var defenderPartyBaseId))
            return;

        var mapEvent = data.Attacker.MapEvent ?? data.Defender.MapEvent;

        mapEventLogger.DebugMapEvent(
            mapEvent,
            "Battle started. AttackerId={AttackerPartyBaseId}, DefenderId={DefenderPartyBaseId}, Attacker={AttackerName}, Defender={DefenderName}",
            attackerPartyBaseId,
            defenderPartyBaseId,
            data.Attacker.GetPartyName(),
            data.Defender.GetPartyName());

        var attackerIsPlayer = data.Attacker.MobileParty?.IsPlayerParty() == true;
        var defenderIsPlayer = data.Defender.MobileParty?.IsPlayerParty() == true;
        var hasPlayer = attackerIsPlayer || defenderIsPlayer;

        if (hasPlayer && AllPlayersInMapEvents())
        {
            mapEventLogger.DebugMapEvent(
                    mapEvent,
                    "All players are in map events. Pausing time on server.");

            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        }

        var message = new NetworkStartBattle(attackerPartyBaseId, defenderPartyBaseId);

        mapEventLogger.DebugMapEvent(
            mapEvent,
            "Sending {MessageType}. AttackerId={AttackerPartyBaseId}, DefenderId={DefenderPartyBaseId}",
            nameof(NetworkStartBattle),
            attackerPartyBaseId,
            defenderPartyBaseId);

        network.SendAll(message);
    }

    private void Handle_NetworkStartBattle(MessagePayload<NetworkStartBattle> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging(message.AttackerId, out PartyBase attacker))
            return;

        if (!objectManager.TryGetObjectWithLogging(message.DefenderId, out PartyBase defender))
            return;

        var mapEvent = attacker.MapEvent ?? defender.MapEvent;

        EncounterManagerPatches.OverrideOnPartyInteraction(attacker, defender);

        mapEventLogger.DebugMapEvent(
            mapEvent,
            "Applied encounter override for network battle. AttackerId={AttackerPartyBaseId}, DefenderId={DefenderPartyBaseId}",
            message.AttackerId,
            message.DefenderId);
    }

    private void Handle_MapEventFinalizeAttempted(MessagePayload<MapEventFinalizeAttempted> payload)
    {

        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Map event finalize attempted. Sending network message to finalize map event on all clients.");

        var message = new NetworkMapEventFinalizeAttempted(mapEventId);
        network.SendAll(message);
    }
    private void Handle_NetworkMapEventFinalizeAttempted(MessagePayload<NetworkMapEventFinalizeAttempted> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(mapEvent, "Handling network map event finalize attempted. Finalizing map event.");

        mapEvent.FinalizeEventAux();

        var message = new NetworkMapEventFinalized();
        network.Send(payload.Who as NetPeer, message);
    }

    private void Handle_NetworkMapEventFinalized(MessagePayload<NetworkMapEventFinalized> payload)
    {
        if (PlayerEncounter.Current != null)
        {
            // TODO determine force out of settlement
            PlayerEncounter.Finish(true);
        }
        
        GameMenu.ExitToLast();
    }

    private bool AllPlayersInMapEvents()
    {
        return playerRegistry.All(player =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.PartyId, out var playerParty))
                return false;

            return playerParty.MapEvent != null;
        });
    }

    private void Handle_PlayerSurrendered(MessagePayload<PlayerSurrendered> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId)) return;

        mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Player surrendered with mobile party {MobilePartyId}", mobilePartyId);

        var message = new NetworkPlayerSurrendered(mobilePartyId, mapEventId);

        network.SendAll(message);

        using (new AllowedThread())
        {
            PlayerEncounter.Current._playerSurrender = true;
            payload.What.MobileParty.BesiegerCamp = null;

            GameMenu.ActivateGameMenu("taken_prisoner");

            PlayerEncounter.Current._stateHandled = true;
        }
    }

    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty mobileParty)) return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network player surrender for mobile party {MobilePartyId} on side {BattleSide}", payload.What.MobilePartyId, mobileParty.Party.Side);

        mapEvent.DoSurrender(mobileParty.Party.Side);
        mapEvent.FinalizeEvent();
    }

    private void Handle_MapEventInvolvedPartiesAdded(MessagePayload<MapEventInvolvedPartiesAdded> payload)
    {
        var message = payload.What;
        if (!objectManager.TryGetIdWithLogging(message.MapEvent, out var mapEventSideId))
            return;

        mapEventLogger.DebugMapEvent(message.MapEvent, "Map event involved parties added. Added party count: {AddedPartyCount}", message.AddedParties.Count());

        var partyIds = new List<string>();

        foreach (var addedParty in message.AddedParties)
        {
            if (objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
            {
                partyIds.Add(mapEventPartyId);
            }
        }

        network.SendAll(new NetworkAddInvolvedParties(
            mapEventSideId,
            partyIds.ToArray()
        ));
    }

    private void Handle_NetworkAddInvolvedParties(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
            return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network add involved parties. Party count: {MapEventPartyCount}", message.MapEventPartyIds.Length);

        using (new AllowedThread())
        {
            foreach (var mapEventPartyId in message.MapEventPartyIds)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(mapEventPartyId, out var mapEventParty))
                    continue;

                mapEventLogger.DebugMapEvent(mapEvent, "Adding involved map event party {MapEventPartyId} to troop upgrade tracker", mapEventPartyId);
                mapEvent.TroopUpgradeTracker.AddParty(mapEventParty);
            }
        }
    }
}