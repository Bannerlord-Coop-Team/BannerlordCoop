using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
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
/// Coop replacement for <c>NavalMissions.OpenNavalBattleMission</c>. Keeps the vanilla naval behavior list but
/// fields only the local player's own party: coop troop suppliers, detached snapshots of own-party ships from
/// <see cref="ICoopShipSnapshotBuilder"/>, own-party captains, no <c>NavalBattleEndLogic</c> (campaign ship capture) and no
/// <c>ShipRetreatLogic</c> (naval retreat is disabled). Other players' and AI hulls are not spawned here.
/// </summary>
public class CoopNavalBattleLauncher : ICoopNavalBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;
    private readonly IBattleAgentBudget agentBudget;
    private readonly ICoopShipSnapshotBuilder shipSnapshotBuilder;
    private readonly IBattleNetwork network;
    private readonly INavalShipEngine shipEngine;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IPlayerManager playerManager;

    public CoopNavalBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher,
        IBattleAgentBudget agentBudget,
        ICoopShipSnapshotBuilder shipSnapshotBuilder,
        IBattleNetwork network,
        INavalShipEngine shipEngine,
        IBattleTeamResolver teamResolver,
        IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
        this.network = network;
        this.shipEngine = shipEngine;
        this.teamResolver = teamResolver;
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
        if (mission == null) return null;

        // Same post-open entry as the field launcher: the controller is attached, so the reserve request it
        // triggers reaches the suppliers registered above.
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        return mission;
    }

    private Mission CreateCoopNavalBattle(MissionInitializerRecord rec, MapEvent mapEvent, string mapEventId)
    {
        bool isPlayerSergeant = mapEvent.IsPlayerSergeant();
        bool isPlayerInArmy = MobileParty.MainParty.Army != null;
        BattleSideEnum playerSide = PartyBase.MainParty.Side;
        bool isPlayerAttacker = playerSide == BattleSideEnum.Attacker;
        var deploymentModel = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;

        var sideParties = mapEvent.GetMapEventSide(playerSide).Parties;
        SplitPlayerSideParties(sideParties, PartyBase.MainParty,
            out var ownMapEventParty, out var ownTeamParties, out var allyTeamParties);
        NavalPlayerDeploymentSlot.Rank = NavalPlayerDeploymentSlot.RankOf(
            sideParties.Select(party => party.Party), PartyBase.MainParty, GetPlayerParties().Contains);
        deploymentModel.GetShipDeploymentLimitsOfPlayerTeams(ownTeamParties, allyTeamParties,
            out var ownTeamLimit, out var allyTeamLimit);
        var enemyLimit = deploymentModel.GetTeamShipDeploymentLimit(mapEvent.GetMapEventSide(playerSide.GetOppositeSide()).Parties);

        var ownCampaignShips = new MBList<IShipOrigin> { deploymentModel.GetSuitablePlayerShip(ownMapEventParty, ownTeamParties) };
        deploymentModel.FillShipsOfTeamParties(ownTeamParties, ownTeamLimit, ownCampaignShips);
        var ownShips = ownCampaignShips.Select(ship => (IShipOrigin)shipSnapshotBuilder.Build((Ship)ship)).ToMBList();
        var captains = OrderOwnCaptains(CoopFieldBattleLauncher.OwnPartyHeroesByPriority(),
            PartyBase.MainParty.LeaderHero?.CharacterObject.StringId, ownShips.Count);

        int deployableOwnShipCount = MathF.Min(ownShips.Count, ownTeamLimit.NetDeploymentLimit);
        var maxDeployableTroopCountPerTeam = new[]
        {
            deploymentModel.GetMaximumDeployableTroopCountForTeam(ownShips, isPlayerTeam: true),
            0,
            0,
        };

        Logger.Information("[NavalBattle] Opening coop naval battle {MapEventId}: side={Side} deploymentRank={Rank} ownShips={ShipCount} " +
            "hulls={Hulls} captains={Captains} deployable={Deployable} crewCapacity={CrewCapacity}",
            mapEventId, playerSide, NavalPlayerDeploymentSlot.Rank, ownShips.Count, string.Join(",", ownShips.Select(ship => ship.Hull.StringId)),
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
                    ownTeamLimit, allyTeamLimit, enemyLimit),
                new NavalTrajectoryPlanningLogic(),
                // Must precede the spawn logic: it pulls every troop from the suppliers once, in EarlyStart.
                new CoopNavalReserveGuard(ownSupplier, playerSide, messageBroker),
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
        AttachNavalServices(mission, playerSide.GetOppositeSide());
        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        Logger.Information("[NavalBattle] Opened coop naval battle for {MapEventId} (player side {Side})", mapEventId, playerSide);
        return mission;
    }

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
    private void AttachNavalServices(Mission mission, BattleSideEnum enemySide)
    {
        var controller = mission.GetMissionBehavior<CoopBattleController>();
        var shipReplicator = new BattleShipReplicator(network, messageBroker, controller.Session, controller.Deployment,
            controller.MissionComponent, shipEngine, teamResolver, objectManager, controller.HostEpochPolicy);
        var stationUseReplicator = new AgentStationUseReplicator(network, messageBroker, controller.Session,
            controller.MissionComponent, shipEngine, controller.Deployment);
        var npcFleetSpawner = new NavalNpcFleetSpawner(controller.Session, shipReplicator, controller.MissionComponent.ShipRegistry,
            shipSnapshotBuilder, objectManager, playerManager, enemySide);
        var shipDamageRouter = new BattleShipDamageRouter(network, messageBroker, controller.Session, controller.MissionComponent,
            shipEngine);
        mission.AddMissionBehavior(new CoopNavalBattleBehavior(shipReplicator, stationUseReplicator, npcFleetSpawner,
            shipDamageRouter));
    }

    // Own party only: vanilla GetMapEventPartiesOfPlayerTeams takes the first non-NPC party as the player's,
    // which is another player's party whenever two players share a side.
    internal static void SplitPlayerSideParties(
        MBReadOnlyList<MapEventParty> sideParties,
        PartyBase ownParty,
        out MapEventParty ownMapEventParty,
        out MBList<MapEventParty> ownTeamParties,
        out MBList<MapEventParty> allyTeamParties)
    {
        ownMapEventParty = null;
        ownTeamParties = new MBList<MapEventParty>();
        allyTeamParties = new MBList<MapEventParty>();
        foreach (var party in sideParties)
        {
            if (party.Party == ownParty)
            {
                ownMapEventParty = party;
                ownTeamParties.Add(party);
                continue;
            }

            allyTeamParties.Add(party);
        }
    }

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
