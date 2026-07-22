using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using ProtoBuf;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class MapEventDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventDebugCommands>();
    private static LateJoinModeFixture lateJoinModeFixture;

    private sealed class LateJoinModeFixture
    {
        public string MapEventId { get; set; }
        public string FirstControllerId { get; set; }
        public string FirstPlayerPartyId { get; set; }
        public string FirstPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData FirstPlayerBehavior { get; set; }
        public string JoiningControllerId { get; set; }
        public string JoiningPlayerPartyId { get; set; }
        public string JoiningPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData JoiningPlayerBehavior { get; set; }
        public string VillageSettlementId { get; set; }
        public float VillageSettlementHitPoints { get; set; }
        public string MilitiaMobilePartyId { get; set; }
        public PartyBehaviorUpdateData MilitiaBehavior { get; set; }
        public bool AllowRaidAiIntervention { get; set; }
        public List<FactionStanceSnapshot> FactionStances { get; set; }
        public bool JoiningPartyJoined { get; set; }
    }

    private sealed class FactionStanceSnapshot
    {
        public IFaction PlayerFaction { get; set; }
        public IFaction VillageFaction { get; set; }
        public StanceType StanceType { get; set; }
    }

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    // coop.debug.mapevent.start_looter
    /// <summary>
    /// Starts combat with looter
    /// </summary>
    [CommandLineArgumentFunction("start_looter", "coop.debug.mapevent")]
    public static string StartRandomLooterMapEvent(List<string> args)
    {
        //if (args.Count != 2)
        //{
        //    return "Usage: coop.debug.besiegercamp.set_number_of_troops_killed_on_side <besiegerCampId> <value> ";
        //}

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject("sea_raiders_1", out PartyBase partyBase))
        {
            return $"BesiegerCamp with ID: sea_raiders_1 not found";
        }

        EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, partyBase);


        return $"MapEvent Started";
    }

    // coop.debug.mapevent.start_nearest_looter
    /// <summary>
    /// Forces an encounter between the player's party and the nearest active bandit/looter party, so
    /// the bandit surrender/recruit dialogue can be reached without chasing one down. Run on a client
    /// (uses the player's main party). Bring a much larger party than the bandits so they offer to
    /// surrender or join.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_looter", "coop.debug.mapevent")]
    public static string StartNearestLooterMapEvent(List<string> args)
    {
        if (!TryGetObjectManager(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
        {
            return "No main party — run this on a client with a player party.";
        }

        var mainPos = mainParty.Position.ToVec2();
        var nearest = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != mainParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(mainPos))
            .FirstOrDefault();

        if (nearest == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        EncounterManager.StartPartyEncounter(mainParty.Party, nearest.Party);

        var partyId = objectManager.TryGetId(nearest, out string registryId) ? registryId : nearest.StringId;

        return $"Started encounter with {nearest.Name} (StringId {nearest.StringId}, registry id {partyId}), " +
               $"{nearest.MemberRoster.TotalManCount} troops, {nearest.Position.ToVec2().Distance(mainPos):0.0} away.";
    }

    // coop.debug.mapevent.start_nearest_bandit_attack PlayerOne
    /// <summary>
    /// Starts a server-authoritative bandit attack encounter against a connected player.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_bandit_attack", "coop.debug.mapevent")]
    public static string StartNearestBanditAttack(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.start_nearest_bandit_attack <controllerId>";
        }

        if (!TryGetObjectManager(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return "Unable to resolve PlayerManager";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player))
        {
            return $"No registered player has controller id {args[0]}.";
        }

        if (!playerManager.IsConnected(player))
        {
            return $"Player {args[0]} is not connected.";
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return $"Unable to resolve player party {player.MobilePartyId}.";
        }

        if (playerParty.MapEvent != null)
        {
            return $"Player {args[0]} is already in a map event.";
        }

        var playerPosition = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != playerParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();

        if (banditParty == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        EncounterManager.StartPartyEncounter(banditParty.Party, playerParty.Party);

        var partyId = objectManager.TryGetId(banditParty, out string registryId)
            ? registryId
            : banditParty.StringId;

        return $"Started attack by {banditParty.Name} (StringId {banditParty.StringId}, registry id {partyId}) " +
               $"against player {args[0]}.";
    }

    // coop.debug.mapevent.late_join_mode_fixture PlayerOne PlayerTwo
    /// <summary>
    /// Creates a server-authoritative battle, claims mission mode before the second player joins, then routes the
    /// second player's join through the real request handler.
    /// </summary>
    [CommandLineArgumentFunction("late_join_mode_fixture", "coop.debug.mapevent")]
    public static string StartLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_fixture <firstControllerId> <joiningControllerId>";
        }

        if (lateJoinModeFixture != null)
        {
            return $"A late-join mode fixture is already active for map event {lateJoinModeFixture.MapEventId}.";
        }

        if (args[0] == args[1])
        {
            return "The fixture requires two different connected players.";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var firstParty, out var error))
        {
            return error;
        }

        if (!TryGetPlayerParty(args[1], requireReady: true, out _, out var joiningParty, out error))
        {
            return error;
        }

        if (firstParty.CurrentSettlement != null || joiningParty.CurrentSettlement != null)
        {
            return "Both players must be on the campaign map, outside settlements.";
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPeer(args[0], out var firstPeer) ||
            !playerManager.TryGetPeer(args[1], out _))
        {
            return "Unable to resolve both connected player peers.";
        }

        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<RaidAiInterventionConfigHandler>(out var raidConfigHandler))
        {
            return "Unable to resolve the late-join mode fixture services.";
        }

        if (!behaviorSnapshot.TryCreate(firstParty, out var firstPlayerBehavior) ||
            !behaviorSnapshot.TryCreate(joiningParty, out var joiningPlayerBehavior))
        {
            return "Unable to capture both players' original movement state.";
        }

        var firstPosition = firstParty.Position.ToVec2();
        var firstFaction = firstParty.MapFaction?.MapFaction ?? firstParty.MapFaction;
        var joiningFaction = joiningParty.MapFaction?.MapFaction ?? joiningParty.MapFaction;
        var village = Settlement.All
            .Where(s => s.IsVillage && s.Village.VillageState == Village.VillageStates.Normal &&
                        s.Party.MapEvent == null && s.MilitiaPartyComponent?.MobileParty?.IsActive == true &&
                        s.MilitiaPartyComponent.MobileParty.MapEvent == null &&
                        s.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount > 0 &&
                        firstFaction != (s.MapFaction?.MapFaction ?? s.MapFaction) &&
                        joiningFaction != (s.MapFaction?.MapFaction ?? s.MapFaction))
            .OrderBy(s => s.Position.ToVec2().DistanceSquared(firstPosition))
            .FirstOrDefault();

        if (village == null)
        {
            return "No normal village with an active militia party was found for both players.";
        }

        var militiaParty = village.MilitiaPartyComponent.MobileParty;
        if (!behaviorSnapshot.TryCreate(militiaParty, out var militiaBehavior))
        {
            return $"Unable to capture the militia movement state for {village.Name}.";
        }

        if (!objectManager.TryGetId(firstParty.Party, out string firstPartyId) ||
            !objectManager.TryGetId(firstParty, out string firstMobilePartyId) ||
            !objectManager.TryGetId(joiningParty.Party, out string joiningPartyId) ||
            !objectManager.TryGetId(joiningParty, out string joiningMobilePartyId) ||
            !objectManager.TryGetId(village, out string villageSettlementId) ||
            !objectManager.TryGetId(militiaParty, out string militiaMobilePartyId))
        {
            return "Unable to resolve fixture party ids.";
        }

        var originalVillageHitPoints = village.SettlementHitPoints;
        var originalAllowRaidAiIntervention = MapEventConfig.AllowRaidAiIntervention;
        var factionStances = new List<FactionStanceSnapshot>();
        var villageFaction = village.MapFaction?.MapFaction ?? village.MapFaction;
        if (!TryApplyTemporaryWarStance(firstFaction, villageFaction, factionStances) ||
            !TryApplyTemporaryWarStance(joiningFaction, villageFaction, factionStances))
        {
            RestoreFactionStances(factionStances);
            return $"Unable to establish temporary hostility against {village.Name}.";
        }

        raidConfigHandler.SetAndBroadcast(true);
        var mapEvent = MapEventBattleFactory.CreateMapEvent(firstParty.Party, village.Party, default);
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            if (mapEvent != null && !mapEvent.IsFinalized)
                mapEvent.FinalizeEvent();

            RestorePartyBehavior(firstParty, firstPlayerBehavior, behaviorSnapshot);
            RestorePartyBehavior(joiningParty, joiningPlayerBehavior, behaviorSnapshot);
            RestorePartyBehavior(militiaParty, militiaBehavior, behaviorSnapshot);
            if (village.Village.VillageState != Village.VillageStates.Normal)
                ChangeVillageStateAction.ApplyBySettingToNormal(village);
            village.SettlementHitPoints = originalVillageHitPoints;
            raidConfigHandler.SetAndBroadcast(originalAllowRaidAiIntervention);
            RestoreFactionStances(factionStances);
            return "Unable to create or resolve the fixture map event.";
        }

        lateJoinModeFixture = new LateJoinModeFixture
        {
            MapEventId = mapEventId,
            FirstControllerId = args[0],
            FirstPlayerPartyId = firstPartyId,
            FirstPlayerMobilePartyId = firstMobilePartyId,
            FirstPlayerBehavior = firstPlayerBehavior,
            JoiningControllerId = args[1],
            JoiningPlayerPartyId = joiningPartyId,
            JoiningPlayerMobilePartyId = joiningMobilePartyId,
            JoiningPlayerBehavior = joiningPlayerBehavior,
            VillageSettlementId = villageSettlementId,
            VillageSettlementHitPoints = originalVillageHitPoints,
            MilitiaMobilePartyId = militiaMobilePartyId,
            MilitiaBehavior = militiaBehavior,
            AllowRaidAiIntervention = originalAllowRaidAiIntervention,
            FactionStances = factionStances,
        };

        var hasVillageMilitiaResistance = mapEvent.EventType == MapEvent.BattleTypes.Raid &&
                                           mapEvent.MapEventSettlement == village &&
                                           mapEvent.DefenderSide?.Parties.Any(
                                               p => p.Party == militiaParty.Party) == true &&
                                           !mapEvent.IsActiveSlowVillageRaid();
        if (!hasVillageMilitiaResistance)
        {
            CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
            return $"Village raid fixture {mapEventId} did not create active militia resistance.";
        }

        // Route the first player's Attack through the real server handler. The resulting mission-start and mode
        // broadcasts reach PlayerTwo before its party belongs to the event, reproducing the missed-claim timing.
        messageBroker.Publish(firstPeer, new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            mapEventId,
            firstMobilePartyId));

        return $"Late-join raid fixture created and first mission requested: mapEvent={mapEventId}, eventType={mapEvent.EventType}, " +
               $"village={village.Name} ({village.StringId}), militiaParty={militiaParty.Name}, " +
               $"firstPlayer={args[0]}, joiningPlayer={args[1]}, firstSide=Attacker.";
    }

    // coop.debug.mapevent.late_join_mode_join
    /// <summary>Routes the waiting player's attacker-side join after the first player has entered the mission.</summary>
    [CommandLineArgumentFunction("late_join_mode_join", "coop.debug.mapevent")]
    public static string JoinLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_join";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";
        if (fixture.JoiningPartyJoined)
            return $"Player {fixture.JoiningControllerId} already joined fixture map event {fixture.MapEventId}.";

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
            !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
        {
            return "Unable to resolve the late-join fixture services.";
        }

        if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
            return $"Player {fixture.FirstControllerId} has not entered the field battle mission.";
        if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
            return $"Player {fixture.JoiningControllerId} is already in a mission.";
        if (!ServerBattleModeArbiter.TryGetMode(fixture.MapEventId, out var mode) ||
            mode != BattleStartMode.Mission)
        {
            return $"Fixture map event {fixture.MapEventId} is not claimed for Mission mode.";
        }
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(fixture.MapEventId, out var mapEvent) ||
            !objectManager.TryGetObjectWithLogging<PartyBase>(fixture.JoiningPlayerPartyId, out var joiningParty))
        {
            return "Unable to resolve the fixture map event or joining party.";
        }

        messageBroker.Publish(joiningPeer, new NetworkRequestJoinBattle(
            fixture.MapEventId,
            fixture.JoiningPlayerPartyId,
            BattleSideEnum.Attacker));

        if (joiningParty.MapEvent != mapEvent)
            return $"Player {fixture.JoiningControllerId} did not join fixture map event {fixture.MapEventId}.";

        fixture.JoiningPartyJoined = true;
        return $"Late join accepted: mapEvent={fixture.MapEventId}, joiningPlayer={fixture.JoiningControllerId}, " +
               "side=Attacker, replayedMode=Mission, firstPlayerInMission=True, joiningPlayerInMission=False.";
    }

    // coop.debug.mapevent.late_join_mode_enter
    /// <summary>Routes the late joiner's Attack request through the real mission-start handler.</summary>
    [CommandLineArgumentFunction("late_join_mode_enter", "coop.debug.mapevent")]
    public static string EnterLateJoinModeFixtureMission(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_enter";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";
        if (!fixture.JoiningPartyJoined)
            return $"Player {fixture.JoiningControllerId} has not joined fixture map event {fixture.MapEventId}.";

        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
            !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
        {
            return "Unable to resolve the late-join mission-entry services.";
        }

        if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
            return $"Player {fixture.FirstControllerId} is no longer in the field battle mission.";
        if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
            return $"Player {fixture.JoiningControllerId} already entered the field battle mission.";

        messageBroker.Publish(joiningPeer, new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            fixture.MapEventId,
            fixture.JoiningPlayerMobilePartyId));

        return $"Late joiner mission requested: mapEvent={fixture.MapEventId}, " +
               $"joiningPlayer={fixture.JoiningControllerId}, mode=Mission.";
    }

#if DEBUG
    // coop.debug.mapevent.late_join_mode_begin_field_battle
    /// <summary>Finishes the local deployment phase so live evidence shows the active field battle.</summary>
    [CommandLineArgumentFunction("late_join_mode_begin_field_battle", "coop.debug.mapevent")]
    public static string BeginLateJoinModeFixtureFieldBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_begin_field_battle";

        var mission = Mission.Current;
        if (mission == null)
            return "No mission is active.";

        var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
        if (deploymentController?.TeamSetupOver != true)
            return "Local deployment is not ready.";

        var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
        if (deploymentHandler == null)
            return "The field battle is already active.";

        deploymentHandler.FinishDeployment();
        return "Local deployment finished; the field battle is active.";
    }

    // coop.debug.mapevent.late_join_mode_exit_missions
    /// <summary>Asks every fixture mission member to return to campaign before authoritative cleanup.</summary>
    [CommandLineArgumentFunction("late_join_mode_exit_missions", "coop.debug.mapevent")]
    public static string ExitLateJoinModeFixtureMissions(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_exit_missions";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";

        if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership))
        {
            return "Unable to resolve the late-join mission-exit services.";
        }

        var requested = 0;
        foreach (var controllerId in new[] { fixture.FirstControllerId, fixture.JoiningControllerId })
        {
            if (!missionMembership.IsControllerInMission(controllerId) ||
                !playerManager.TryGetPeer(controllerId, out var peer))
                continue;

            network.Send(peer, new NetworkEndLateJoinModeFixtureMission(fixture.MapEventId));
            requested++;
        }

        return $"Late-join fixture mission exit requested for {requested} player(s).";
    }
#endif

    // coop.debug.mapevent.late_join_mode_state PlayerTwo
    /// <summary>Reports a player's map-event membership and known authoritative battle mode.</summary>
    [CommandLineArgumentFunction("late_join_mode_state", "coop.debug.mapevent")]
    public static string GetLateJoinModeState(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_state <controllerId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var mapEvent = playerParty.MapEvent;
        var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out string resolvedId)
            ? resolvedId
            : "none";
        var eventType = mapEvent?.EventType.ToString() ?? "none";
        var village = mapEvent?.MapEventSettlement;
        var villageName = village != null ? $"{village.Name} ({village.StringId})" : "none";
        var militiaResistance = mapEvent?.DefenderSide?.Parties.Any(
            p => p.Party?.MobileParty?.IsMilitia == true) == true;
        var side = playerParty.MapEventSide?.MissionSide.ToString() ?? "none";
        var mode = "Unclaimed";
        if (mapEventId != "none")
        {
            if (ModInformation.IsServer && ServerBattleModeArbiter.TryGetMode(mapEventId, out var serverMode))
                mode = serverMode.ToString();
            else if (BattleModeRegistry.IsMission(mapEventId))
                mode = BattleStartMode.Mission.ToString();
            else if (BattleModeRegistry.IsSimulation(mapEventId))
                mode = BattleStartMode.Simulation.ToString();
        }

        var missionActive = ModInformation.IsServer
            ? ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) &&
              missionMembership.IsControllerInMission(args[0])
            : MissionState.Current != null || Mission.Current != null;
        var missionAgents = ModInformation.IsClient && Mission.Current != null
            ? Mission.Current.Agents.Count
            : 0;
        var deploymentActive = ModInformation.IsClient &&
                               Mission.Current?.HasMissionBehavior<DeploymentHandler>() == true;

        return $"Late-join mode state: controller={args[0]}, mapEvent={mapEventId}, eventType={eventType}, " +
               $"village={villageName}, militiaResistance={militiaResistance}, side={side}, mode={mode}, " +
               $"missionActive={missionActive}, missionAgents={missionAgents}, deploymentActive={deploymentActive}.";
    }

    // coop.debug.mapevent.late_join_mode_cleanup
    /// <summary>Removes the fixture raid and restores the village, militia, parties, and raid config.</summary>
    [CommandLineArgumentFunction("late_join_mode_cleanup", "coop.debug.mapevent")]
    public static string CleanupLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 0)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_cleanup";
        }

        if (lateJoinModeFixture == null)
        {
            return "No late-join mode fixture is active.";
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return "Unable to resolve the late-join mode cleanup services.";
        }

        var mapEventId = lateJoinModeFixture.MapEventId;
        var restored = CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
        return restored
            ? $"Late-join raid fixture {mapEventId} cleaned up and village raid state restored."
            : $"Late-join raid fixture {mapEventId} cleaned up, but its original state could not be fully restored.";
    }

    private static bool CleanupLateJoinModeFixture(
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        var fixture = lateJoinModeFixture;
        if (fixture == null) return true;

        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.JoiningPlayerPartyId));
        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.FirstPlayerPartyId));
        if (objectManager.TryGetObject<MapEvent>(fixture.MapEventId, out var mapEvent) && !mapEvent.IsFinalized)
            mapEvent.FinalizeEvent();
        ServerBattleModeArbiter.Release(fixture.MapEventId);

        var restored = RestorePartyBehavior(
            fixture.FirstPlayerMobilePartyId,
            fixture.FirstPlayerBehavior,
            behaviorSnapshot,
            objectManager);
        restored = RestorePartyBehavior(
            fixture.JoiningPlayerMobilePartyId,
            fixture.JoiningPlayerBehavior,
            behaviorSnapshot,
            objectManager) && restored;
        restored = RestorePartyBehavior(
            fixture.MilitiaMobilePartyId,
            fixture.MilitiaBehavior,
            behaviorSnapshot,
            objectManager) && restored;
        restored = RestoreVillage(
            fixture.VillageSettlementId,
            fixture.VillageSettlementHitPoints,
            objectManager) && restored;
        restored = RestoreFactionStances(fixture.FactionStances) && restored;

        if (ContainerProvider.TryResolve<RaidAiInterventionConfigHandler>(out var raidConfigHandler))
        {
            MapEventConfig.AllowRaidAiIntervention = fixture.AllowRaidAiIntervention;
            raidConfigHandler.SetAndBroadcast(fixture.AllowRaidAiIntervention);
        }
        else
        {
            restored = false;
        }

        lateJoinModeFixture = null;
        return restored;
    }

    private static bool RestoreVillage(
        string settlementId,
        float settlementHitPoints,
        IObjectManager objectManager)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement))
            return false;

        if (settlement.Village.VillageState != Village.VillageStates.Normal)
            ChangeVillageStateAction.ApplyBySettingToNormal(settlement);
        settlement.SettlementHitPoints = settlementHitPoints;
        return settlement.Village.VillageState == Village.VillageStates.Normal &&
               settlement.SettlementHitPoints == settlementHitPoints;
    }

    private static bool TryApplyTemporaryWarStance(
        IFaction playerFaction,
        IFaction villageFaction,
        List<FactionStanceSnapshot> snapshots)
    {
        if (playerFaction == null || villageFaction == null || playerFaction == villageFaction)
            return false;

        if (snapshots.Any(snapshot => snapshot.PlayerFaction == playerFaction &&
                                      snapshot.VillageFaction == villageFaction))
            return true;

        var stanceLink = FactionManager.Instance.GetStanceLinkInternal(playerFaction, villageFaction);
        snapshots.Add(new FactionStanceSnapshot
        {
            PlayerFaction = playerFaction,
            VillageFaction = villageFaction,
            StanceType = stanceLink.StanceType,
        });
        VillageHostileFactionStanceHelper.ApplyWarStance(playerFaction, villageFaction);
        return VillageHostileFactionStanceHelper.HasWarStance(playerFaction, villageFaction);
    }

    private static bool RestoreFactionStances(List<FactionStanceSnapshot> snapshots)
    {
        var restored = true;
        foreach (var snapshot in snapshots)
        {
            FactionManager.SetStance(snapshot.PlayerFaction, snapshot.VillageFaction, snapshot.StanceType);
            var stanceLink = FactionManager.Instance.GetStanceLinkInternal(
                snapshot.PlayerFaction,
                snapshot.VillageFaction);
            stanceLink.StanceType = snapshot.StanceType;
            snapshot.PlayerFaction.UpdateFactionsAtWarWith();
            snapshot.VillageFaction.UpdateFactionsAtWarWith();

            var shouldBeAtWar = snapshot.StanceType == StanceType.War;
            restored = VillageHostileFactionStanceHelper.HasWarStance(
                snapshot.PlayerFaction,
                snapshot.VillageFaction) == shouldBeAtWar && restored;
        }
        return restored;
    }

    private static bool RestorePartyBehavior(
        string mobilePartyId,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(mobilePartyId, out var mobileParty))
            return false;

        return RestorePartyBehavior(mobileParty, behavior, behaviorSnapshot);
    }

    private static bool RestorePartyBehavior(
        MobileParty mobileParty,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        mobileParty.Position = behavior.PartyPosition;
        return behaviorSnapshot.TryApply(mobileParty, behavior, out _);
    }

    // coop.debug.mapevent.peace_pursuit_fixture PlayerOne
    /// <summary>
    /// Finds a neutral AI party that can be used without changing its original movement state.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_fixture", "coop.debug.mapevent")]
    public static string GetPeacePursuitFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_fixture <controllerId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = FindPeacePursuitFixture(playerParty);
        if (neutralParty == null)
        {
            return "No active neutral AI party already holding on the map.";
        }

        return FormatPeacePursuitState("Peace pursuit fixture", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.peace_pursuit_state PlayerOne mobileParty_1
    /// <summary>
    /// Reports the pursuit-test party state on the current machine.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_state", "coop.debug.mapevent")]
    public static string GetPeacePursuitState(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_state <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        return FormatPeacePursuitState("Peace pursuit state", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.test_peace_stops_pursuit PlayerOne mobileParty_1
    /// <summary>
    /// Makes a selected neutral AI party pursue a connected player, then makes peace.
    /// </summary>
    [CommandLineArgumentFunction("test_peace_stops_pursuit", "coop.debug.mapevent")]
    public static string TestPeaceStopsPursuit(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.test_peace_stops_pursuit <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        if (!IsPeacePursuitFixture(neutralParty, playerParty))
        {
            return $"Party {args[1]} is not a neutral AI party already holding on the map.";
        }

        DeclareWarAction.ApplyByDefault(neutralParty.MapFaction, playerParty.MapFaction);
        if (!FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction))
        {
            return $"Unable to establish war between {neutralParty.MapFaction.Name} and {playerParty.MapFaction.Name}.";
        }

        neutralParty.SetMoveGoAroundParty(playerParty, MobileParty.NavigationType.Default);
        MakePeaceAction.Apply(neutralParty.MapFaction, playerParty.MapFaction);

        var stopped = neutralParty.DefaultBehavior == AiBehavior.Hold &&
                      neutralParty.PartyMoveMode == MoveModeType.Hold &&
                      neutralParty.TargetParty == null &&
                      !FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction);

        return FormatPeacePursuitState($"Peace pursuit test {(stopped ? "passed" : "failed")}",
            objectManager,
            neutralParty,
            playerParty);
    }

    private static bool TryGetPlayerParty(
        string controllerId,
        bool requireReady,
        out IObjectManager objectManager,
        out MobileParty playerParty,
        out string error)
    {
        objectManager = null;
        playerParty = null;
        error = null;

        if (!TryGetObjectManager(out objectManager))
        {
            error = "Unable to resolve ObjectManager";
            return false;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve PlayerManager";
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"No registered player has controller id {controllerId}.";
            return false;
        }

        if (requireReady && ModInformation.IsServer && !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (requireReady && playerParty.MapEvent != null)
        {
            error = $"Player {controllerId} is already in a map event.";
            return false;
        }

        if (playerParty.MapFaction == null)
        {
            error = $"Player {controllerId} has no map faction.";
            return false;
        }

        return true;
    }

    private static MobileParty FindPeacePursuitFixture(MobileParty playerParty)
    {
        var playerPosition = playerParty.Position.ToVec2();
        return MobileParty.All
            .Where(p => IsPeacePursuitFixture(p, playerParty))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
    }

    private static bool IsPeacePursuitFixture(MobileParty party, MobileParty playerParty)
    {
        return party.IsActive &&
               !party.IsBandit &&
               !party.IsPlayerParty() &&
               party != playerParty &&
               party.MapEvent == null &&
               party.CurrentSettlement == null &&
               party.MemberRoster.TotalManCount > 0 &&
               party.MapFaction != null &&
               party.MapFaction != playerParty.MapFaction &&
               !FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction) &&
               party.DefaultBehavior == AiBehavior.Hold &&
               party.PartyMoveMode == MoveModeType.Hold &&
               party.TargetParty == null;
    }

    private static string FormatPeacePursuitState(
        string prefix,
        IObjectManager objectManager,
        MobileParty party,
        MobileParty playerParty)
    {
        var registryId = objectManager.TryGetId(party, out string partyId) ? partyId : "none";
        var target = party.TargetParty == null ? "none" : party.TargetParty.StringId;
        var atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction);
        var mapEvent = party.MapEvent == null ? "none" : party.MapEvent.ToString();

        return $"{prefix}: party={party.StringId}, registryId={registryId}, behavior={party.DefaultBehavior}, " +
               $"moveMode={party.PartyMoveMode}, target={target}, atWar={atWar}, mapEvent={mapEvent}.";
    }

    /// <summary>
    /// Kills a random troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_random_troop", "coop.debug.mapevent")]
    public static string KillRandomTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        var party = enemySide.Parties[MBRandom.RandomInt(enemySide.Parties.Count)];
        if (party is null)
        {
            return "Enemy side has no parties";
        }

        var troops = party.Troops;
        if (troops is null || troops.Count() == 0)
        {
            return "Enemy party has no troops";
        }

        var entries = troops._elementDictionary.ToArray();

        if (entries.Length == 0)
        {
            return "Enemy party has no troops";
        }

        var randomEntry = entries[MBRandom.RandomInt(entries.Length)];

        UniqueTroopDescriptor descriptor = randomEntry.Key;
        FlattenedTroopRosterElement troopElement = randomEntry.Value;

        try
        {
            enemySide.OnTroopKilled(descriptor);
        }
        catch (Exception ex)
        {
            return $"Failed to kill random troop: {ex.Message}";
        }

        return $"Killed random troop: {troopElement.Troop?.Name}";
    }

    /// <summary>
    /// Kills all but one troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_all_but_one", "coop.debug.mapevent")]
    public static string KillAllButOneTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        if (enemySide.Parties is null || enemySide.Parties.Count == 0)
        {
            return "Enemy side has no parties";
        }

        var allTroops = new List<(MapEventParty Party, UniqueTroopDescriptor Descriptor, FlattenedTroopRosterElement Element)>();

        foreach (var party in enemySide.Parties)
        {
            if (party?.Troops?._elementDictionary is null)
                continue;

            foreach (var entry in party.Troops._elementDictionary)
            {
                var descriptor = entry.Key;
                var element = entry.Value;

                allTroops.Add((party, descriptor, element));
            }
        }

        if (allTroops.Count == 0)
        {
            return "Enemy side has no troops";
        }

        if (allTroops.Count == 1)
        {
            return $"Enemy side already has only one troop: {allTroops[0].Element.Troop?.Name}";
        }

        var survivorIndex = MBRandom.RandomInt(allTroops.Count);
        var survivor = allTroops[survivorIndex];

        var killedCount = 0;

        for (var i = 0; i < allTroops.Count; i++)
        {
            if (i == survivorIndex)
                continue;

            try
            {
                enemySide.OnTroopKilled(allTroops[i].Descriptor);
                killedCount++;
            }
            catch (Exception ex)
            {

            }
        }

        return $"Killed {killedCount} troops. Survivor: {survivor.Element.Troop?.Name}";
    }

    /// <summary>
    /// Lists the fields and properties of the current PlayerEncounter.
    /// </summary>
    [CommandLineArgumentFunction("list_player_encounter", "coop.debug.mapevent")]
    public static string ListPlayerEncounter(List<string> args)
    {
        var playerEncounter = PlayerEncounter.Current;
        if (playerEncounter == null)
        {
            return "No current PlayerEncounter";
        }

        var sb = new StringBuilder();

        sb.AppendLine("PlayerEncounter:");
        AppendObjectDetails(sb, playerEncounter, "\t", "PlayerEncounter Details");

        var result = sb.ToString();

        Logger.Debug("{PlayerEncounter}", result);

        return result;
    }

    /// <summary>
    /// Prints a compact, teardown-focused snapshot of the current <see cref="PlayerEncounter"/> and the main
    /// party's map-event state. Run on each client after a battle to spot an encounter that did not tear down —
    /// e.g. PlayerEncounter.Current still PRESENT, or MainParty.MapEvent lingering on an already-finalized event.
    /// Unlike <c>list_player_encounter</c> (full reflection dump) this is short enough to diff across clients.
    /// </summary>
    [CommandLineArgumentFunction("encounter_state", "coop.debug.mapevent")]
    public static string EncounterState(List<string> args)
    {
        TryGetObjectManager(out var objectManager);

        var sb = new StringBuilder();

        var encounter = PlayerEncounter.Current;
        sb.AppendLine($"PlayerEncounter.Current: {(encounter == null ? "<null> (torn down)" : "PRESENT")}");
        if (encounter != null)
        {
            sb.AppendLine($"\tBattle:           {FormatMapEvent(PlayerEncounter.Battle, objectManager)}");
            sb.AppendLine($"\t_mapEvent:        {FormatMapEvent(encounter._mapEvent, objectManager)}");
            sb.AppendLine($"\tEncounteredParty: {FormatPartyBase(PlayerEncounter.EncounteredParty)}");
            sb.AppendLine($"\t_attackerParty:   {FormatPartyBase(encounter._attackerParty)}");
            sb.AppendLine($"\t_defenderParty:   {FormatPartyBase(encounter._defenderParty)}");
        }

        var mainParty = MobileParty.MainParty;
        sb.AppendLine($"MainParty.MapEvent:      {FormatMapEvent(mainParty?.MapEvent, objectManager)}");

        var side = mainParty?.Party?.MapEventSide;
        if (side == null)
            sb.AppendLine("MainParty.MapEventSide:  <null>");
        else
            sb.AppendLine($"MainParty.MapEventSide:  leader={FormatPartyBase(side.LeaderParty)} mainPartyIsLeader={side.LeaderParty == mainParty?.Party}");

        sb.AppendLine($"CurrentMenu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");
        sb.AppendLine($"MissionState.Current:    {(MissionState.Current == null ? "<null>" : "PRESENT")}");

        var result = sb.ToString();
        Logger.Debug("{EncounterState}", result);
        return result;
    }

    private static string FormatMapEvent(MapEvent mapEvent, IObjectManager objectManager)
    {
        if (mapEvent == null) return "<null>";

        var id = "<no id>";
        if (objectManager != null && objectManager.TryGetId(mapEvent, out var resolved))
            id = resolved;

        return $"id={id} finalized={mapEvent.IsFinalized} state={mapEvent.BattleState} winner={mapEvent.WinningSide}";
    }

    [CommandLineArgumentFunction("get_events", "coop.debug.mapevent")]
    public static string GetEvents(List<string> args)
    {
        var sb = new StringBuilder();

        if(!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        foreach(var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (objectManager.TryGetIdWithLogging(mapEvent, out var id))
            {
                sb.AppendLine($"Map event id: {id}");
            }

            var partyNames = mapEvent.AttackerSide.Parties?
                .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
                .ToArray() ?? Array.Empty<string>();
            sb.AppendLine($"\tAttacker: {string.Join(",", FormatSideNames(mapEvent.AttackerSide))}");
            sb.AppendLine($"\tDefender: {string.Join(",", FormatSideNames(mapEvent.DefenderSide))}");
        }

        return sb.ToString();
    }

    private static string[] FormatSideNames(MapEventSide side)
    {
        if (side == null)
            return new string[] { "<null>" };

        return side.Parties?
            .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
            .ToArray() ?? Array.Empty<string>();
    }

    [CommandLineArgumentFunction("get_event", "coop.debug.mapevent")]
    public static string GetEvent(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.get_event <mapEventId>";
        }

        if (!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        var mapEventId = args[0];

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
        {
            return $"Failed to find MapEvent with id: {mapEventId}";
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Map event id: {mapEventId}");
        sb.AppendLine();

        AppendMapEventSummary(sb, mapEvent);
        sb.AppendLine();

        var result = sb.ToString();

        Logger.Debug("{MapEvent}", result);

        return result;
    }

    private static void AppendMapEventSummary(StringBuilder sb, MapEvent mapEvent)
    {
        sb.AppendLine("Summary:");

        AppendSideSummary(sb, "Attacker", mapEvent.AttackerSide);
        AppendSideSummary(sb, "Defender", mapEvent.DefenderSide);
    }

    private static void AppendSideSummary(StringBuilder sb, string sideName, MapEventSide side)
    {
        if (side == null)
        {
            sb.AppendLine($"\t{sideName}: <null>");
            return;
        }

        sb.AppendLine($"\t{sideName}: {string.Join(", ", FormatSideNames(side))}");

        AppendObjectDetails(sb, side, "\t\t", "Side Details");

        sb.AppendLine("\t\tParties:");

        var parties = side.Parties;
        if (parties == null)
        {
            sb.AppendLine("\t\t\t<null>");
            return;
        }

        var index = 0;
        foreach (var party in parties)
        {
            sb.AppendLine($"\t\t\tParty[{index}]:");

            if (party == null)
            {
                sb.AppendLine("\t\t\t\t<null>");
            }
            else
            {
                AppendMapEventPartyDetails(sb, party, "\t\t\t\t");
            }

            index++;
        }
    }
    private static void AppendMapEventPartyDetails(StringBuilder sb, MapEventParty party, string indent)
    {
        var partyName = party.Party?.Name?.ToString() ?? "<null>";
        sb.AppendLine($"{indent}Party: {partyName}");

        AppendObjectDetails(sb, party, indent, "MapEventParty Details");
    }

    private static void AppendObjectDetails(StringBuilder sb, object obj, string indent, string title)
    {
        if (obj == null)
        {
            sb.AppendLine($"{indent}{title}: <null>");
            return;
        }

        var type = obj.GetType();

        sb.AppendLine($"{indent}{title}: {GetFriendlyTypeName(type)}");

        AppendFields(sb, obj, type, indent + "\t");
        AppendProperties(sb, obj, type, indent + "\t");
    }

    private static void AppendFields(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Fields:");

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var field in fields.OrderBy(f => f.Name))
        {
            object value;

            try
            {
                value = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{field.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{field.Name}: {FormatValue(value)}");
        }
    }

    private static void AppendProperties(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Properties:");

        var properties = type.GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (properties.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var property in properties.OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <indexed property>");
                continue;
            }

            object value;

            try
            {
                value = property.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{property.Name}: {FormatValue(value)}");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "<null>";

        if (value is string str)
            return str;

        if (value is TextObject textObject)
            return textObject.ToString();

        if (value is CharacterObject character)
            return FormatCharacter(character);

        if (value is MobileParty mobileParty)
            return FormatMobileParty(mobileParty);

        if (value is PartyBase partyBase)
            return FormatPartyBase(partyBase);

        if (value is IFaction faction)
            return faction.Name?.ToString() ?? faction.StringId ?? "<unnamed faction>";

        if (value is UniqueTroopDescriptor descriptor)
            return descriptor.ToString();

        if (value is IEnumerable enumerable && !(value is string))
            return FormatEnumerable(enumerable);

        return value.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var values = new List<string>();
        var count = 0;

        foreach (var item in enumerable)
        {
            if (count >= 20)
            {
                values.Add("...");
                break;
            }

            values.Add(FormatValue(item));
            count++;
        }

        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatCharacter(CharacterObject character)
    {
        if (character == null)
            return "<null>";

        var id = character.StringId ?? "<no id>";
        var name = character.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatMobileParty(MobileParty party)
    {
        if (party == null)
            return "<null>";

        var id = party.StringId ?? "<no id>";
        var name = party.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatPartyBase(PartyBase party)
    {
        if (party == null)
            return "<null>";

        var name = party.Name?.ToString() ?? "<no name>";

        return name;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return genericTypeName + "<" + string.Join(", ", genericArguments) + ">";
    }
}

#if DEBUG
/// <summary>[Server -&gt; Client] Ends a live-test fixture mission without resolving its campaign battle.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEndLateJoinModeFixtureMission : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkEndLateJoinModeFixtureMission(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}

/// <summary>Applies the server's live-test fixture mission-exit request on participating clients.</summary>
internal sealed class LateJoinModeFixtureMissionExitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public LateJoinModeFixtureMissionExitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    private void Handle(MessagePayload<NetworkEndLateJoinModeFixtureMission> payload)
    {
        if (ModInformation.IsServer)
            return;

        var mapEventId = payload.What.MapEventId;
        GameThread.RunSafe(() =>
        {
            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var localMapEventId) ||
                localMapEventId != mapEventId)
                return;

            var mission = Mission.Current ?? MissionState.Current?.CurrentMission;
            mission?.EndMission();
        }, context: nameof(NetworkEndLateJoinModeFixtureMission));
    }
}
#endif
