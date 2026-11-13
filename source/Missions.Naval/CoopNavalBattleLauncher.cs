using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
#endif
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Battles;
using NavalDLC;
using NavalDLC.Missions;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.Handlers;
using NavalDLC.Missions.MissionLogics;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace Missions.Naval;

/// <summary>
/// Coop replacement for <c>NavalMissions.OpenNavalBattleMission</c>: opens vanilla's naval behaviour list fielding only the
/// local player's own party, its snapshot ships and captains; other players' and AI hulls are not spawned here.
/// </summary>
public class CoopNavalBattleLauncher : ICoopNavalBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;
    private readonly IBattleAgentBudget agentBudget;
    private readonly ICoopShipSnapshotBuilder shipSnapshotBuilder;
    private readonly INavalShipEngine shipEngine;
    private readonly INavalSteeredCopies steeredCopies;
    private readonly INavalShipCaptureEngine captureEngine;
    private readonly INavalBattleServicesFactory servicesFactory;
    private readonly IPlayerManager playerManager;
    // A fresh failure log per behaviour, so each counts its own throws.
    private readonly Func<INavalTickFailureLog> tickFailureLogs;
    private readonly IAsyncAgentTick asyncAgentTick;
    private readonly INavalLateHullDeployment lateHullDeployment;
    private readonly INavalBallistaTargets ballistaTargets;
    private readonly INavalMissionTracker missionTracker;
    private readonly INavalForeignHullRegistry foreignHulls;
    private readonly INavalRopeRegistry ropes;
    private readonly INavalShipDamageGate damageGate;
    private readonly INavalPlayerDeploymentSlotTracker deploymentSlot;
    private readonly INavalShipBudget shipBudget;
    private readonly Func<IBattleSession, IBattleShipReplicator, INavalNpcFleetRollback, BattleSideEnum, INavalNpcFleetSpawner> npcFleetSpawners;
#if DEBUG
    private readonly INavalScenarioState scenarioState;
    private readonly INavalDebugHulls debugHulls;
    private readonly CopySteeringKnobs steeringKnobs;
#endif

    public CoopNavalBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher,
        IBattleAgentBudget agentBudget,
        ICoopShipSnapshotBuilder shipSnapshotBuilder,
        INavalSteeredCopies steeredCopies,
        // The engine and the behaviour must share one per-battle copy set; a plain engine injection would track copies nobody steers.
        Func<INavalSteeredCopies, INavalShipEngine> shipEngines,
        INavalShipCaptureEngine captureEngine,
        INavalBattleServicesFactory servicesFactory,
        IPlayerManager playerManager,
        Func<INavalTickFailureLog> tickFailureLogs,
        IAsyncAgentTick asyncAgentTick,
        INavalLateHullDeployment lateHullDeployment,
        INavalBallistaTargets ballistaTargets,
        INavalMissionTracker missionTracker,
        INavalForeignHullRegistry foreignHulls,
        INavalRopeRegistry ropes,
        INavalShipDamageGate damageGate,
        INavalPlayerDeploymentSlotTracker deploymentSlot,
        INavalShipBudget shipBudget,
        // One AI fleet spawner per battle side, over that battle's session and ship replicator.
        Func<IBattleSession, IBattleShipReplicator, INavalNpcFleetRollback, BattleSideEnum, INavalNpcFleetSpawner> npcFleetSpawners
#if DEBUG
        , INavalScenarioState scenarioState,
        INavalDebugHulls debugHulls,
        CopySteeringKnobs steeringKnobs
#endif
        )
    {
#if DEBUG
        this.scenarioState = scenarioState;
        this.debugHulls = debugHulls;
        this.steeringKnobs = steeringKnobs;
#endif
        this.playerManager = playerManager;
        this.tickFailureLogs = tickFailureLogs;
        this.asyncAgentTick = asyncAgentTick;
        this.lateHullDeployment = lateHullDeployment;
        this.ballistaTargets = ballistaTargets;
        this.missionTracker = missionTracker;
        this.foreignHulls = foreignHulls;
        this.ropes = ropes;
        this.damageGate = damageGate;
        this.deploymentSlot = deploymentSlot;
        this.shipBudget = shipBudget;
        this.npcFleetSpawners = npcFleetSpawners;
        // One battle's copies, shared by the engine that tracks them and the behaviour that steers them.
        this.steeredCopies = steeredCopies;
        shipEngine = shipEngines(steeredCopies);
        this.captureEngine = captureEngine;
        this.servicesFactory = servicesFactory;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorAttacher = behaviorAttacher;
        this.agentBudget = agentBudget;
        this.shipSnapshotBuilder = shipSnapshotBuilder;
    }

    public Mission OpenCoopNavalBattle(MissionInitializerRecord rec)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[NavalBattle] Cannot open coop naval battle: no resolvable map event");
            return null;
        }

        if (MobileParty.MainParty.Ships.Count == 0)
        {
            Logger.Error("[NavalBattle] Cannot open coop naval battle for {MapEventId}: the main party has no ships", mapEventId);
            return null;
        }

        var mission = CreateCoopNavalBattle(rec, mapEvent, mapEventId);

        // Same post-open entry as the field launcher: the controller is attached, so the reserve request it
        // triggers reaches the suppliers registered above.
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        return mission;
    }

    // Opens the naval mission with the own party's ships, captains and slot, then attaches the coop and naval behaviours.
    private Mission CreateCoopNavalBattle(MissionInitializerRecord rec, MapEvent mapEvent, string mapEventId)
    {
        bool isPlayerSergeant = mapEvent.IsPlayerSergeant();
        bool isPlayerInArmy = MobileParty.MainParty.Army != null;
        BattleSideEnum playerSide = PartyBase.MainParty.Side;
        bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
        var deploymentModel = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;

        var sideParties = mapEvent.GetMapEventSide(playerSide).Parties;
        var playerParties = GetPlayerParties();
        deploymentSlot.Rank = NavalPlayerDeploymentSlot.RankOf(
            sideParties.Select(party => party.Party), PartyBase.MainParty, playerParties.Contains);
        deploymentSlot.PlayerCount = NavalPlayerDeploymentSlot.PlayerCountOf(
            sideParties.Select(party => party.Party), playerParties.Contains);
        var enemyLimit = deploymentModel.GetTeamShipDeploymentLimit(mapEvent.GetMapEventSide(playerSide.GetOppositeSide()).Parties);

        // The battle host's AI share of this side subtracts this same fleet, so both come from the budget.
        var ownFleet = shipBudget.PlayerFleet(sideParties, PartyBase.MainParty);
        var ownShips = ownFleet.Ships.Select(ship => (IShipOrigin)shipSnapshotBuilder.Build((Ship)ship)).ToMBList();
        var captains = OrderOwnCaptains(CoopFieldBattleLauncher.OwnPartyHeroesByPriority(),
            PartyBase.MainParty.LeaderHero?.CharacterObject.StringId, ownShips.Count);

        int deployableOwnShipCount = ownFleet.FieldedCount;
        var maxDeployableTroopCountPerTeam = new[]
        {
            deploymentModel.GetMaximumDeployableTroopCountForTeam(ownShips, isPlayerTeam: true),
            0,
            0,
        };

        Logger.Information("[NavalBattle] Opening coop naval battle {MapEventId}: side={Side} deploymentRank={Rank}/{PlayerCount} ownShips={ShipCount} " +
            "hulls={Hulls} captains={Captains} deployable={Deployable} crewCapacity={CrewCapacity}",
            mapEventId, playerSide, deploymentSlot.Rank, deploymentSlot.PlayerCount, ownShips.Count, string.Join(",", ownShips.Select(ship => ship.Hull.StringId)),
            string.Join(",", captains), deployableOwnShipCount, maxDeployableTroopCountPerTeam[0]);

        rec.AtmosphereOnCampaign.NauticalInfo.UsesNavalSimulatedWater = 1;
        var mission = NavalMissionState.OpenNew("NavalBattle", rec, naval =>
        {
            var defenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, objectManager, agentBudget);
            var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager, agentBudget);
            CoopTroopSupplierRegistry.Register(defenderSupplier);
            CoopTroopSupplierRegistry.Register(attackerSupplier);
            var ownSupplier = isPlayerAttacker ? attackerSupplier : defenderSupplier;

            return new MissionBehavior[]
            {
                new NavalShipsLogic(),
                new NavalFloatsamLogic(),
                new NavalAgentsLogic(),
                new DefaultNavalMissionLogic(ownShips, new MBList<IShipOrigin>(), new MBList<IShipOrigin>(),
                    ownFleet.PlayerLimit, ownFleet.AllyLimit, enemyLimit),
                new NavalTrajectoryPlanningLogic(),
                // Must precede the spawn logic: it pulls every troop from the suppliers once, in EarlyStart.
                new CoopNavalSetupGuard(ownSupplier, playerSide, messageBroker),
                new DefaultNavalMissionAgentSpawnLogic(new IMissionTroopSupplier[] { defenderSupplier, attackerSupplier },
                    playerSide, deployableOwnShipCount, maxDeployableTroopCountPerTeam),
                new NavalMissionDeploymentPlanningLogic(naval),
                new BattlePowerCalculationLogic(),
                new NavalBattleAgentLogic(),
                new WaveParametersComputerLogic(),
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new NavalAgentMoraleInteractionLogic(),
                new NavalMissionCombatantsLogic(mapEvent.InvolvedParties, PartyBase.MainParty,
                    mapEvent.GetLeaderParty(BattleSideEnum.Defender), mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                    Mission.MissionTeamAITypeEnum.NavalBattle, isPlayerSergeant),
                new BattleObserverMissionLogic(),
                new AgentHumanAILogic(),
                new AgentVictoryLogic(),
                new ShipCollisionOutcomeLogic(naval),
                new ShipRetreatLogic(),
                new NavalBoundaryForceFieldLogic(),
                new BattleMissionAgentInteractionLogic(),
                new NavalAssignPlayerRoleInTeamMissionController(!isPlayerSergeant, isPlayerSergeant, isPlayerInArmy, captains),
                new EquipmentControllerLeaveLogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(30f),
                new HighlightsController(),
                new BattleHighlightsController(),
                new NavalDeploymentMissionController(isPlayerAttacker),
                new NavalDeploymentHandler(isPlayerAttacker),
            };
        });

        behaviorAttacher.Attach(mission);
        AttachNavalServices(mission);
#if DEBUG
        var debugController = mission.GetMissionBehavior<CoopBattleController>();
        mission.AddMissionBehavior(new NavalCopyProbeLogic(debugController.MissionComponent.ShipRegistry, steeredCopies, steeringKnobs));
        mission.AddMissionBehavior(new NavalWaterProbeLogic(debugController.Session, debugController.MissionComponent.ShipRegistry,
            tickFailureLogs(), foreignHulls));
        AttachScenario(mission, mapEventId);
#endif
        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        Logger.Information("[NavalBattle] Opened coop naval battle for {MapEventId} (player side {Side})", mapEventId, playerSide);
        return mission;
    }

    // The parties of every connected player that resolve on this client.
    private HashSet<PartyBase> GetPlayerParties()
    {
        var parties = new HashSet<PartyBase>();
        foreach (var player in playerManager.Players)
        {
            if (objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                parties.Add(party.Party);
        }

        return parties;
    }

    // The ship services share the attached controller's per-battle session, deployment and mission component.
    private void AttachNavalServices(Mission mission)
    {
        var context = mission.GetMissionBehavior<CoopBattleController>().Context;
        var services = servicesFactory.Create(context, shipEngine, captureEngine, asyncAgentTick);
        // A spawner per side on every client, so either side's attempt has an owner here, also for an AI party joining later.
        var npcFleets = new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker }
            .Select(side => npcFleetSpawners(context.Session, services.ShipReplicator,
                new NavalNpcFleetRollback(services.ShipReplicator), side))
            .ToList();
        mission.AddMissionBehavior(new CoopNavalBattleBehavior(services, steeredCopies, npcFleets,
            mission.GetMissionBehavior<NavalShipsLogic>(), tickFailureLogs(), tickFailureLogs(), lateHullDeployment,
            ballistaTargets, missionTracker, foreignHulls, ropes, damageGate, deploymentSlot));
        var tallyReader = new NavalSideTallyReader(shipEngine, services.ConditionStream, services.MigrationReserveFielder);
        mission.AddMissionBehavior(new CoopNavalBattleEndLogic(context.Session, context.Deployment, context.ResultCommitter,
            npcFleets, services.HullLedger, tallyReader,
            new NavalPlayerHeroGrace(context.Session, context.MissionContext, playerManager, objectManager)));
    }

#if DEBUG
    // Runs the client steps of a DEBUG naval scenario of this battle; attached to every battle, since a returner's re-sent
    // scenario can arrive after another player's mission start pulled it in (R41a-2 E).
    private void AttachScenario(Mission mission, string mapEventId)
    {
        var controller = mission.GetMissionBehavior<CoopBattleController>();
        mission.AddMissionBehavior(new NavalScenarioMissionLogic(scenarioState, debugHulls, controller.Session,
            controller.MissionComponent.ShipRegistry, messageBroker, mapEventId, damageGate));
    }
#endif

    // Vanilla GetOrderedCaptainsForPlayerTeamShips seats each ship's party leader first and fills the rest by
    // priority. With only the own party's ships that is: the leader, then the other own heroes, one per ship.
    internal static List<string> OrderOwnCaptains(IReadOnlyList<string> ownHeroesByPriority, string leaderHeroId, int shipCount)
    {
        var captains = new List<string>(shipCount);
        if (leaderHeroId != null && ownHeroesByPriority.Contains(leaderHeroId))
            captains.Add(leaderHeroId);

        captains.AddRange(ownHeroesByPriority.Where(heroId => heroId != leaderHeroId));
        if (captains.Count > shipCount)
            captains.RemoveRange(shipCount, captains.Count - shipCount);
        return captains;
    }
}
