using Common;
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
using TaleWorlds.Diamond;
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

        messageBroker.Subscribe<PlayerLeaveBattle>(Handle_PlayerLeaveBattle);
        messageBroker.Subscribe<NetworkLeavePlayerBattle>(Handle_NetworkLeavePlayerBattle);

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

        messageBroker.Unsubscribe<PlayerLeaveBattle>(Handle_PlayerLeaveBattle);
        messageBroker.Unsubscribe<NetworkLeavePlayerBattle>(Handle_NetworkLeavePlayerBattle);

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

        int randomTerrainSeed = MBRandom.RandomInt(10000);
        float damageToFriends = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        float damageFromPlayerToFriends = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();

        var message = new NetworkStartAttackMission(randomTerrainSeed, damageToFriends, damageFromPlayerToFriends);
        network.SendAll(message);
    }

    private void Handle_NetworkStartAttackMission(MessagePayload<NetworkStartAttackMission> payload)
    {
        var msg = payload.What;
        var battle = PlayerEncounter.Battle;
        bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
        CampaignVec2 position = MobileParty.MainParty.Position;

        IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
        MapPatchData mapPatchAtPosition = mapSceneWrapper.GetMapPatchAtPosition(position);

        string battleScene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
        MissionInitializerRecord rec2 = new MissionInitializerRecord(battleScene);
        TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
        rec2.TerrainType = (int)faceTerrainType2;
        rec2.DamageToFriendsMultiplier = msg.DamageToFriendsMultiplier;
        rec2.DamageFromPlayerToFriendsMultiplier = msg.DamageFromPlayerToFriendsMultiplier;
        rec2.NeedsRandomTerrain = false;
        rec2.PlayingInCampaignMode = true;
        rec2.RandomTerrainSeed = msg.RandomTerrainSeed;
        rec2.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position);
        rec2.SceneHasMapPatch = true;
        rec2.DecalAtlasGroup = 2;
        rec2.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
        position = battle.AttackerSide.LeaderParty.Position;
        Vec2 v2 = position.ToVec2();
        position = battle.DefenderSide.LeaderParty.Position;
        rec2.PatchEncounterDir = (v2 - position.ToVec2()).Normalized();

        using (new AllowedThread())
        {
            CampaignMission.OpenBattleMission(rec2);
        }
    }

    private void Handle_BattleStarted(MessagePayload<BattleStarted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Attacker, out var attackerPartyBaseId))
            return;

        if (!objectManager.TryGetIdWithLogging(data.Defender, out var defenderPartyBaseId))
            return;

        var mapEvent = data.Attacker.MapEvent ?? data.Defender.MapEvent;

        if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId))
            return;

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

        //if (hasPlayer && AllPlayersInMapEvents())
        //{
        //    mapEventLogger.DebugMapEvent(
        //            mapEvent,
        //            "All players are in map events. Pausing time on server.");

        //    timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        //}

        var message = new NetworkStartBattle(mapEventId, attackerPartyBaseId, defenderPartyBaseId);

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

        if (!objectManager.TryGetObjectWithLogging(message.MapEventId, out MapEvent mapEvent))
            return;

        if (!objectManager.TryGetObjectWithLogging(message.AttackerId, out PartyBase attacker))
            return;

        if (!objectManager.TryGetObjectWithLogging(message.DefenderId, out PartyBase defender))
            return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                using (new AllowedThread())
                {
                    if (defender.IsMobile)
                    {
                        if (attacker.MobileParty.IsPartyControlled() == true)
                        {
                            InformationManager.DisplayMessage(new InformationMessage("Started encounter"));
                        }
                        EncounterManager.StartPartyEncounter(attacker, defender);

                        return;
                    }
                    if (defender.IsSettlement)
                    {
                        EncounterManager.StartSettlementEncounter(attacker.MobileParty, defender.Settlement);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start party encounter");
            }
        });

        mapEventLogger.DebugMapEvent(
            mapEvent,
            "Applied encounter override for network battle. AttackerId={AttackerPartyBaseId}, DefenderId={DefenderPartyBaseId}",
            message.AttackerId,
            message.DefenderId);
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

    private void Handle_PlayerLeaveBattle(MessagePayload<PlayerLeaveBattle> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId)) return;

        mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Player leaving battle with mobile party {MobilePartyId}", mobilePartyId);

        network.SendAll(new NetworkLeavePlayerBattle(mobilePartyId, mapEventId));

        using (new AllowedThread())
        {
            payload.What.MapEvent.FinalizeEvent();

            GameMenu.ExitToLast();

            Campaign.Current.PlayerEncounter = null;
            Campaign.Current.LocationEncounter = null;
        }
    }

    private void Handle_NetworkLeavePlayerBattle(MessagePayload<NetworkLeavePlayerBattle> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty playerParty)) return;

        mapEventLogger.DebugMapEvent(mapEvent, "Handling network player leave battle for mobile party {MobilePartyId}", payload.What.MobilePartyId);

        Settlement currentSettlement = playerParty.CurrentSettlement;
        int numberOfInvolvedMen = mapEvent.GetNumberOfInvolvedMen(playerParty.Party.Side);
        bool forcePlayerOutFromSettlement;
        if (currentSettlement?.SiegeEvent != null)
        {
            forcePlayerOutFromSettlement = currentSettlement?.MapFaction != playerParty.MapFaction;
        }
        else
        {
            forcePlayerOutFromSettlement = true;
        }

        playerParty.TeleportPartyToOutSideOfEncounterRadius();

        var enemyParties = mapEvent._sides.Single(side => !side.Parties.Any(x => x.Party == playerParty.Party));

        foreach (var mapEventParty in enemyParties.Parties)
        {
            var disableTime = DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.GetOrCreateValue(mapEventParty.Party.MobileParty.Ai);

            disableTime[playerParty] = CampaignTime.HoursFromNow(2);
        }

        mapEventLogger.DebugMapEvent(mapEvent, "Finalizing map event after network player leave");
        mapEvent.FinalizeEvent();

        if (playerParty.CurrentSettlement != null && playerParty.AttachedTo == null && forcePlayerOutFromSettlement)
        {
            LeaveSettlementAction.ApplyForParty(playerParty);
        }

        if (playerParty.BesiegerCamp != null)
        {
            playerParty.BesiegerCamp = null;
        }
        if (mapEvent != null && !mapEvent.IsFinalized && !mapEvent.IsRaid && numberOfInvolvedMen == playerParty.Party.NumberOfHealthyMembers)
        {
            //MapEvent mapEvent2 = mapEvent;
            //PlayerEncounter playerEncounter = PlayerEncounter.Current;
            //FlattenedTroopRoster[] priorTroops;
            //if (playerEncounter == null)
            //{
            //    priorTroops = null;
            //}
            //else
            //{
            //    BattleSimulation battleSimulation = mapEvent.BattleSimulation;
            //    priorTroops = ((battleSimulation != null) ? battleSimulation.SelectedTroops : null);
            //}

            // TODO sync simulation
            //mapEvent2.SimulateBattleSetup(priorTroops);
            //mapEvent.SimulateBattleRound((playerParty.Party.Side == BattleSideEnum.Attacker) ? 1 : 0, (playerParty.Party.Side == BattleSideEnum.Attacker) ? 0 : 1);
        }
        if (currentSettlement != null)
        {
            EncounterManager.StartSettlementEncounter(playerParty, currentSettlement);
        }
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