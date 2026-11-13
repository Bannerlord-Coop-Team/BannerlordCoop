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
/// [Game thread, battle host] Fields one side's AI fleet late, the way vanilla's naval start does: its ships, crew, formations,
/// side plan, hulls and team crewing, from the NPC reserve the server sent this battle host.
/// </summary>
public interface INavalNpcFleetFielder
{
    /// <summary>The side's NPC reserve here: its supplier and the parties in it no player owns that still have troops.</summary>
    NavalNpcReserve ReadReserve(BattleSideEnum side);

    /// <summary>
    /// Fields the side's AI fleet from <paramref name="reserve"/>, recording each hull and pulled troop in <paramref name="work"/>
    /// as it happens; returns why a step couldn't go on (<see cref="NavalNpcFleetFielder.NoShipsLeft"/> when the players'
    /// fleets take all of the side's ships), or null once the fleet is crewed.
    /// </summary>
    string Field(Mission mission, BattleSideEnum side, NavalNpcReserve reserve, NavalNpcFleetAttemptWork work);
}

/// <inheritdoc cref="INavalNpcFleetFielder"/>
public class NavalNpcFleetFielder : INavalNpcFleetFielder
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalNpcFleetFielder>();

    // AssignShipsToFormations (DefaultNavalMissionLogic) uses the first eight formations.
    private const int MaxShipFormations = 8;

    /// <summary>Why <see cref="Field"/> fields nothing when the players' fleets take all of the side's ships, a normal outcome.</summary>
    public const string NoShipsLeft = "the players' fleets take all of the side's ships";

    private readonly IBattleSession session;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INavalLateDeploymentPlanTracker latePlans;
    private readonly IBattleTeamResolver teamResolver;
    private readonly INavalShipBudget shipBudget;
    private readonly INavalPlayerDeploymentSlotTracker playerSlots;

    // Takes the battle's session and ship replicator, the vanilla spawn helpers and the own player slot rank.
    public NavalNpcFleetFielder(IBattleSession session, IBattleShipReplicator shipReplicator, ICoopShipSnapshotBuilder snapshotBuilder,
        IObjectManager objectManager, IPlayerManager playerManager, INavalLateDeploymentPlanTracker latePlans,
        IBattleTeamResolver teamResolver, INavalShipBudget shipBudget, INavalPlayerDeploymentSlotTracker playerSlots)
    {
        this.session = session;
        this.shipReplicator = shipReplicator;
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.latePlans = latePlans;
        this.teamResolver = teamResolver;
        this.shipBudget = shipBudget;
        this.playerSlots = playerSlots;
    }

    /// <summary>Vanilla's ship count before crewing: the deployment limit, the ships and the free ship formations.</summary>
    internal static int ShipCount(int deploymentLimit, int ships, int freeFormations) =>
        Math.Max(0, Math.Min(Math.Min(deploymentLimit, ships), Math.Min(freeFormations, MaxShipFormations)));

    // Reads the side's supplier and its AI parties once, for the spawner's decision and the fielding that follows.
    public NavalNpcReserve ReadReserve(BattleSideEnum side)
    {
        var supplier = SideSupplier(side);
        return new NavalNpcReserve(supplier, NpcParties(supplier));
    }

    // Picks the ships, crew and formations the way vanilla's naval start does, then spawns and crews them.
    public string Field(Mission mission, BattleSideEnum side, NavalNpcReserve reserve, NavalNpcFleetAttemptWork work)
    {
        var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
        var agentsLogic = mission.GetMissionBehavior<NavalAgentsLogic>();
        var navalLogic = mission.GetMissionBehavior<DefaultNavalMissionLogic>();
        var planning = mission.GetMissionBehavior<NavalMissionDeploymentPlanningLogic>();
        if (shipsLogic == null || agentsLogic == null || navalLogic == null || planning == null)
            return "naval mission behaviors missing";

        // The team every peer puts this side's AI hulls on, so the host's own allies go on its ally team.
        var team = teamResolver.ResolveAiFleetTeam(side);
        if (team == null) return "no team for the side's AI hulls";

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent)) return "map event not found";

        var parties = reserve.Parties;
        work.Players = shipBudget.PlayerFleets(mapEvent.GetMapEventSide(side).Parties);
        var limit = shipBudget.AiLimit(work.Players, parties);
        // The players' fleets can take all of the side's eight ships, which just leaves it no AI fleet.
        if (limit.NetDeploymentLimit == 0) return NoShipsLeft;

        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        var campaignShips = new MBList<IShipOrigin>();
        model.FillShipsOfTeamParties(parties, limit, campaignShips);
        var ships = campaignShips.OfType<Ship>().Select(ship => (snapshot: snapshotBuilder.Build(ship), owner: ship.Owner)).ToList();
        var formations = team.FormationsIncludingEmpty
            .Where(formation => (int)formation.FormationIndex < MaxShipFormations
                && !shipsLogic.GetShipAssignment(team.TeamSide, formation.FormationIndex).IsSet)
            .ToList();

        // Crew only for the hulls that can field, so troops past their capacity stay in the reserve instead of being pulled; a
        // hull needs at least one crew, so the pulled troops also cap how many field.
        int planned = ShipCount(limit.NetDeploymentLimit, ships.Count, formations.Count);
        var plannedShips = ships.Take(planned).Select(ship => (IShipOrigin)ship.snapshot).ToMBList();
        int crewCapacity = model.GetMaximumDeployableTroopCountForTeam(plannedShips, isPlayerTeam: false);
        var troops = PullTroops(reserve.Supplier, parties, crewCapacity);
        work.Troops.AddRange(troops);
        int count = Math.Min(planned, troops.Count);
        Logger.Information("[NavalBattle] Fielding the AI fleet of {MapEventId}: side={Side} team={Team} parties={Parties} ships={Ships} " +
            "limit={Limit} troops={Troops} freeFormations={Formations} count={Count}",
            session.InstanceId, side, team.TeamSide, parties.Count, ships.Count, limit.NetDeploymentLimit, troops.Count, formations.Count,
            count);
        if (count == 0) return "no ship to field";

        AssignAndSpawnHulls(mission, shipsLogic, navalLogic, planning, side, team, formations.Take(count).ToList(), ships, parties, work);
        if (work.Hulls.Count == 0) return "no hull spawned";

        CrewFleet(agentsLogic, team, troops, work);
        return null;
    }

    // Assigns a ship to each formation, makes the side's late plan and spawns the hulls; an assignment left without a hull
    // is cleared.
    private void AssignAndSpawnHulls(Mission mission, NavalShipsLogic shipsLogic, DefaultNavalMissionLogic navalLogic,
        NavalMissionDeploymentPlanningLogic planning, BattleSideEnum side, Team team, List<Formation> formations,
        List<(Ship snapshot, PartyBase owner)> ships, MBList<MapEventParty> parties, NavalNpcFleetAttemptWork work)
    {
        var fleet = formations.Zip(ships, (formation, ship) => new FleetShip(formation, ship.snapshot, ship.owner)).ToList();
        foreach (var ship in fleet)
            shipsLogic.SetShipAssignment(team.TeamSide, ship.Formation.FormationIndex, ship.Snapshot);

        try
        {
            // The AI team was planned with no ships at deployment start, so it is planned now: the host's own allies on their
            // own, an enemy fleet with vanilla's plan for its side, which skips a team that already has one.
            if (team == mission.PlayerAllyTeam)
                PlanAllyTeam(mission, shipsLogic, planning, side, team, fleet, work.Players.PlayerCount);
            else
                latePlans.MakeForSide(navalLogic, side);
            SpawnAssignedHulls(shipsLogic, planning, team, fleet, parties, work);
        }
        finally
        {
            // An assignment without a hull would count as a ship in every later plan and spawn query.
            foreach (var ship in fleet)
            {
                if (shipsLogic.GetShipAssignment(team.TeamSide, ship.Formation.FormationIndex).MissionShip == null)
                    shipsLogic.ClearShipAssignment(team.TeamSide, (int)ship.Formation.FormationIndex);
            }
        }
    }

    // Plans the allied AI fleet alone ahead of the front-most of the side's players now, a joiner since the host opened included,
    // and says if the slot didn't fit.
    private void PlanAllyTeam(Mission mission, NavalShipsLogic shipsLogic, NavalMissionDeploymentPlanningLogic planning,
        BattleSideEnum side, Team team, List<FleetShip> fleet, int playerCount)
    {
        float allyRange = NavalSideDeploymentSlots.LongestTeamRange(fleet.Select(ship =>
            shipsLogic.GetShipAssignment(team.TeamSide, ship.Formation.FormationIndex).MissionShipObject.DeploymentArea.y));
        var (slot, opposingBase) = NavalSideDeploymentSlots.AllySlot(mission.GetInitialSpawnPathData(side),
            ownRank: playerSlots.Rank,
            ownPlanOffset: planning.GetSpawnPathOffset(mission.PlayerTeam),
            playerCount: playerCount,
            allyRange: allyRange,
            gameRange: NavalSideDeploymentSlots.LongestTeamRangeOfGame());
        var aiShips = fleet.Select(ship => (ship.Formation.FormationIndex, (IShipOrigin)ship.Snapshot));
        latePlans.MakeForAllyTeam(planning, team, aiShips, slot.Offset, opposingBase);

        switch (slot.Problem)
        {
            case NavalSlotProblem.NoRoomOnPath:
                Logger.Warning("[NavalBattle] The {Side} spawn path has no free room for the allied AI fleet ahead of the player " +
                    "fleets; it is planned at {Slot} and can overlap a player fleet", side, slot.Offset);
                break;
            case NavalSlotProblem.HeldShortOfOpposingBase:
                Logger.Warning("[NavalBattle] The allied AI fleet of the {Side} side would reach the opposing base {OpposingBase}; " +
                    "it is held at {Slot}, closer to the player fleets than the gap", side, opposingBase, slot.Offset);
                break;
        }
    }

    // The naval spawn logic's team crewing (AllocateAndDeployInitialTroopsOfTeam), with the late origins.
    private static void CrewFleet(NavalAgentsLogic agentsLogic, Team team, MBList<IAgentOriginBase> troops,
        NavalNpcFleetAttemptWork work)
    {
        var teamSide = team.TeamSide;
        work.BeginCrewing(teamSide, agentsLogic.GetSpawnReinforcementsOnTick(teamSide));
        agentsLogic.AddTroopOrigins(teamSide, troops);
        agentsLogic.AutoComputeDesiredTroopCountsPerShip(teamSide);
        agentsLogic.AssignTroops(teamSide);
        agentsLogic.InitializeReinforcementTimers(teamSide);
        agentsLogic.SetSpawnReinforcementsOnTick(teamSide, true);
        work.FirstCrewBatch = agentsLogic.SpawnNextBatch(teamSide);
#if DEBUG
        work.ThrowIfArmed(NavalFleetFailStage.DuringCrewing);
#endif
        agentsLogic.AssignAndTeleportCrewToShipMachines(teamSide);
        Logger.Information("[NavalBattle] Fielded {Hulls} AI hull(s) and a first crew batch of {Troops} on {Team}",
            work.Hulls.Count, work.FirstCrewBatch, team.TeamSide);
    }

    // Spawns each assigned formation's hull at its plan frame, under AI control, and registers it for the peers. Each hull is
    // recorded as soon as it exists, so a later throw still takes it back.
    private void SpawnAssignedHulls(NavalShipsLogic shipsLogic, NavalMissionDeploymentPlanningLogic planning, Team team,
        List<FleetShip> fleet, MBList<MapEventParty> parties, NavalNpcFleetAttemptWork work)
    {
        foreach (var ship in fleet)
        {
            var formation = ship.Formation;
            var plan = planning.GetFormationPlan(team, formation.FormationIndex);
            if (!plan.HasFrame())
            {
                Logger.Error("[NavalBattle] No deployment frame for AI formation {Formation}; its ship is not fielded", formation.FormationIndex);
                continue;
            }

            var frame = plan.GetFrame();
            var hull = shipsLogic.SpawnShip(formation, in frame, spawnAnchored: false, checkForFreeArea: false);
            if (hull == null) continue;

            work.Hulls.Add(hull);
            hull.SetController(NavalDLC.Missions.ShipControl.ShipControllerType.AI);
            formation.SetControlledByAI(true);
            shipReplicator.RegisterNpcHull(hull, formation, PartyIdOf(parties, ship.Owner));
#if DEBUG
            work.ThrowIfArmed(NavalFleetFailStage.AfterFirstHull);
#endif
        }
    }

    // One AI formation with the ship snapshot it fields and the party that owns the ship.
    private readonly struct FleetShip
    {
        public readonly Formation Formation;
        public readonly Ship Snapshot;
        public readonly PartyBase Owner;

        // Pairs the formation with its ship and owner.
        public FleetShip(Formation formation, Ship snapshot, PartyBase owner)
        {
            Formation = formation;
            Snapshot = snapshot;
            Owner = owner;
        }
    }

    // The side's supplier of this battle; it holds the NPC reserve once the server sent it to the battle host, next to the host's
    // own party on the host's side.
    private CoopTroopSupplier SideSupplier(BattleSideEnum side) =>
        CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId).FirstOrDefault(supplier => supplier.Side == side);

    // The reserve's parties with troops left that no player owns.
    private MBList<MapEventParty> NpcParties(CoopTroopSupplier supplier)
    {
        var parties = new MBList<MapEventParty>();
        if (supplier == null) return parties;

        foreach (var (partyId, remaining) in supplier.GetRemainingByParty())
        {
            if (remaining <= 0 || !objectManager.TryGetObject<MapEventParty>(partyId, out var party)) continue;
            if (party.Party == null || playerManager.Contains(party.Party.MobileParty)) continue;
            parties.Add(party);
        }

        return parties;
    }

    // Round-robin over the NPC parties up to vanilla's crew capacity, not SupplyOneTroop: the host's reserve also holds the
    // party of a player who dropped from the battle (BattleTroopReserveBuilder), and its troops stay off the AI fleet.
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

    // The reserve id of the party that owns a fielded ship, for the hull's spawn record and fate report.
    private string PartyIdOf(MBList<MapEventParty> parties, PartyBase owner)
    {
        var party = parties.FirstOrDefault(candidate => candidate.Party == owner);
        return party != null && objectManager.TryGetId(party, out var id) ? id : null;
    }
}
