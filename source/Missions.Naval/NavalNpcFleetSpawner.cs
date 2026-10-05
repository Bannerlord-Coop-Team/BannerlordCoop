using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Battles;
using NavalDLC;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread, battle host] Fields the AI (NPC) enemy fleet late. A coop naval battle opens with an empty enemy
/// ship list because the NPC reserve only reaches the elected host after the mission loaded, so once the host has it
/// and its deployment is over, this runs vanilla's late path: <c>FillShipsOfTeamParties</c>, <c>SetShipAssignment</c>,
/// the side's deployment plan and <c>SpawnShip</c>, then crews the hulls the way the naval spawn logic crews a team.
/// </summary>
public interface INavalNpcFleetSpawner
{
    /// <summary>[Game thread] Spawns the fleet once its conditions hold.</summary>
    void Tick();

    /// <summary>Spawner state (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="INavalNpcFleetSpawner"/>
public class NavalNpcFleetSpawner : INavalNpcFleetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalNpcFleetSpawner>();

    // AssignShipsToFormations (DefaultNavalMissionLogic) uses the first eight formations.
    private const int MaxShipFormations = 8;

    internal const string WaitingForHost = "waiting_for_host";
    internal const string WaitingForDeployment = "waiting_for_deployment";
    internal const string WaitingForReserve = "waiting_for_reserve";
    internal const string AlreadyFielded = "already_fielded";
    internal const string Ready = "ready";
    internal const string Fielded = "fielded";
    internal const string Failed = "failed";

    private readonly IBattleSession session;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly INetworkShipRegistry registry;
    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly BattleSideEnum enemySide;

    private bool done;
    private string state = WaitingForHost;
    private int hullsSpawned;
    private int troopsFielded;
    private string failure;

    public NavalNpcFleetSpawner(
        IBattleSession session,
        IBattleShipReplicator shipReplicator,
        INetworkShipRegistry registry,
        ICoopShipSnapshotBuilder snapshotBuilder,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        BattleSideEnum enemySide)
    {
        this.session = session;
        this.shipReplicator = shipReplicator;
        this.registry = registry;
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.enemySide = enemySide;
    }

    /// <summary>
    /// Whether the fleet can be fielded now. Only the battle host fields AI, only after its deployment (vanilla's
    /// reinforcement-time spawn path), only with the NPC reserve in hand, and never when AI hulls already exist
    /// (a promoted host inherits them).
    /// </summary>
    internal static string Decide(bool isLocalHost, bool deploymentFinished, bool reservePopulated, int npcParties, bool npcHullsRegistered)
    {
        if (!isLocalHost) return WaitingForHost;
        if (npcHullsRegistered) return AlreadyFielded;
        if (!deploymentFinished) return WaitingForDeployment;
        if (!reservePopulated || npcParties == 0) return WaitingForReserve;
        return Ready;
    }

    /// <summary>Vanilla's ship count: the deployment limit, the ships, the crew and the ship formations.</summary>
    internal static int ShipCount(int deploymentLimit, int ships, int troops, int freeFormations) =>
        Math.Max(0, Math.Min(Math.Min(deploymentLimit, ships), Math.Min(troops, Math.Min(freeFormations, MaxShipFormations))));

    public void Tick()
    {
        var mission = Mission.Current;
        if (done || mission == null) return;

        bool isLocalHost = session.IsLocalHost;
        bool deploymentFinished = mission.IsDeploymentFinished;
        bool npcHullsRegistered = isLocalHost && registry.Ships.Any(ship => ship.IsNpcParty);
        var supplier = isLocalHost && deploymentFinished && !npcHullsRegistered ? EnemySupplier() : null;
        bool reservePopulated = supplier?.IsPopulated == true;
        var parties = reservePopulated ? NpcParties(supplier) : new MBList<MapEventParty>();
        state = Decide(isLocalHost, deploymentFinished, reservePopulated, parties.Count, npcHullsRegistered);
        if (state == AlreadyFielded)
        {
            done = true;
            return;
        }

        if (state != Ready) return;

        // One attempt: it consumes reserve troops, so a failure is reported once instead of retried every tick.
        done = true;
        try
        {
            SpawnFleet(mission, supplier, parties);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            Logger.Error(exception, "[NavalBattle] Could not field the AI fleet of {MapEventId}", session.InstanceId);
        }

        state = Outcome(hullsSpawned);
    }

    internal static string Outcome(int hullsSpawned) => hullsSpawned > 0 ? Fielded : Failed;

    private void SpawnFleet(Mission mission, CoopTroopSupplier supplier, MBList<MapEventParty> parties)
    {
        var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
        var agentsLogic = mission.GetMissionBehavior<NavalAgentsLogic>();
        var navalLogic = mission.GetMissionBehavior<DefaultNavalMissionLogic>();
        var planning = mission.GetMissionBehavior<NavalMissionDeploymentPlanningLogic>();
        var team = BattleTeams.Resolve(enemySide);
        if (shipsLogic == null || agentsLogic == null || navalLogic == null || planning == null || team == null)
        {
            failure = "naval mission behaviors or enemy team missing";
            return;
        }

        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        var limit = model.GetTeamShipDeploymentLimit(parties);
        var campaignShips = new MBList<IShipOrigin>();
        model.FillShipsOfTeamParties(parties, limit, campaignShips);
        var ships = campaignShips.OfType<Ship>().Select(ship => (snapshot: snapshotBuilder.Build(ship), owner: ship.Owner)).ToList();
        var snapshots = ships.Select(ship => (IShipOrigin)ship.snapshot).ToMBList();

        var troops = PullTroops(supplier, parties, model.GetMaximumDeployableTroopCountForTeam(snapshots, isPlayerTeam: false));
        var formations = team.FormationsIncludingEmpty
            .Where(formation => (int)formation.FormationIndex < MaxShipFormations
                && !shipsLogic.GetShipAssignment(team.TeamSide, formation.FormationIndex).IsSet)
            .ToList();
        int count = ShipCount(limit.NetDeploymentLimit, ships.Count, troops.Count, formations.Count);
        Logger.Information("[NavalBattle] Fielding the AI fleet of {MapEventId}: side={Side} parties={Parties} ships={Ships} " +
            "limit={Limit} troops={Troops} freeFormations={Formations} count={Count}",
            session.InstanceId, enemySide, parties.Count, ships.Count, limit.NetDeploymentLimit, troops.Count, formations.Count, count);
        if (count == 0)
        {
            failure = "no ship to field";
            return;
        }

        for (int index = 0; index < count; index++)
            shipsLogic.SetShipAssignment(team.TeamSide, formations[index].FormationIndex, ships[index].snapshot);

        try
        {
            // The enemy side was planned with no ships at deployment start, so its plan is made now, after the fact.
            NavalLateDeploymentPlan.MakeForSide(navalLogic, enemySide);
            SpawnAssignedHulls(shipsLogic, planning, team, formations.Take(count).ToList(), ships, parties);
        }
        finally
        {
            // An assignment without a hull would count as a ship in every later plan and spawn query.
            foreach (var formation in formations.Take(count))
            {
                if (shipsLogic.GetShipAssignment(team.TeamSide, formation.FormationIndex).MissionShip == null)
                    shipsLogic.ClearShipAssignment(team.TeamSide, (int)formation.FormationIndex);
            }
        }

        if (hullsSpawned == 0)
        {
            failure = "no hull spawned";
            return;
        }

        // The naval spawn logic's team crewing (AllocateAndDeployInitialTroopsOfTeam), with the late origins.
        var teamSide = team.TeamSide;
        agentsLogic.AddTroopOrigins(teamSide, troops);
        agentsLogic.AutoComputeDesiredTroopCountsPerShip(teamSide);
        agentsLogic.AssignTroops(teamSide);
        agentsLogic.InitializeReinforcementTimers(teamSide);
        agentsLogic.SetSpawnReinforcementsOnTick(teamSide, true);
        troopsFielded = agentsLogic.SpawnNextBatch(teamSide);
        agentsLogic.AssignAndTeleportCrewToShipMachines(teamSide);
        Logger.Information("[NavalBattle] Fielded {Hulls} AI hull(s) and a first crew batch of {Troops} on {Team}",
            hullsSpawned, troopsFielded, team.TeamSide);
    }

    private void SpawnAssignedHulls(NavalShipsLogic shipsLogic, NavalMissionDeploymentPlanningLogic planning, Team team,
        List<Formation> formations, List<(Ship snapshot, PartyBase owner)> ships, MBList<MapEventParty> parties)
    {
        for (int index = 0; index < formations.Count; index++)
        {
            var formation = formations[index];
            var plan = planning.GetFormationPlan(team, formation.FormationIndex);
            if (!plan.HasFrame())
            {
                Logger.Error("[NavalBattle] No deployment frame for AI formation {Formation}; its ship is not fielded", formation.FormationIndex);
                continue;
            }

            var frame = plan.GetFrame();
            var hull = shipsLogic.SpawnShip(formation, in frame, spawnAnchored: false, checkForFreeArea: false);
            if (hull == null) continue;

            hull.SetController(NavalDLC.Missions.ShipControl.ShipControllerType.AI);
            formation.SetControlledByAI(true);
            shipReplicator.RegisterNpcHull(hull, formation, PartyIdOf(parties, ships[index].owner));
            hullsSpawned++;
        }
    }

    private CoopTroopSupplier EnemySupplier() =>
        CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId).FirstOrDefault(supplier => supplier.Side == enemySide);

    // The reserve's parties this host owns that no player owns.
    private MBList<MapEventParty> NpcParties(CoopTroopSupplier supplier)
    {
        var playerParties = new HashSet<PartyBase>();
        foreach (var player in playerManager.Players)
        {
            if (objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                playerParties.Add(party.Party);
        }

        var parties = new MBList<MapEventParty>();
        foreach (var (partyId, remaining) in supplier.GetRemainingByParty())
        {
            if (remaining <= 0 || !objectManager.TryGetObject<MapEventParty>(partyId, out var party)) continue;
            if (party.Party == null || playerParties.Contains(party.Party)) continue;
            parties.Add(party);
        }

        return parties;
    }

    // Round-robin over the parties, up to vanilla's crew capacity for the team's ships.
    private MBList<IAgentOriginBase> PullTroops(CoopTroopSupplier supplier, MBList<MapEventParty> parties, int maximum)
    {
        var partyIds = parties.Select(party => objectManager.TryGetId(party, out var id) ? id : null).Where(id => id != null).ToList();
        var troops = new MBList<IAgentOriginBase>();
        while (troops.Count < maximum && partyIds.Count > 0)
        {
            for (int index = partyIds.Count - 1; index >= 0 && troops.Count < maximum; index--)
            {
                var origin = supplier.SupplyOneTroopFromParty(partyIds[index]);
                if (origin == null) partyIds.RemoveAt(index);
                else troops.Add(origin);
            }
        }

        return troops;
    }

    private string PartyIdOf(MBList<MapEventParty> parties, PartyBase owner)
    {
        var party = parties.FirstOrDefault(candidate => candidate.Party == owner);
        return party != null && objectManager.TryGetId(party, out var id) ? id : null;
    }

    public object Inspect() => new
    {
        enemySide = enemySide.ToString(),
        state,
        hullsSpawned,
        firstCrewBatch = troopsFielded,
        failure,
    };
}
