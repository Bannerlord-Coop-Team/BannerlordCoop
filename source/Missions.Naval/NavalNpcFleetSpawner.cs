using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Battles;
using Missions.Messages;
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
/// [Game thread] Fields the AI (NPC) enemy fleet late, once per battle and on the battle host only, because the NPC reserve
/// reaches the elected host after the mission loaded. Every client keeps the attempt's outcome.
/// </summary>
public interface INavalNpcFleetSpawner : IDisposable
{
    /// <summary>[Game thread] Spawns the fleet once its conditions hold.</summary>
    void Tick();

    /// <summary>The side whose fleet this spawner tried to field and could not, otherwise <see cref="BattleSideEnum.None"/>.</summary>
    BattleSideEnum FailedSide { get; }

    /// <summary>Spawner state (diagnostics).</summary>
    object Inspect();
}

/// <summary>Runs vanilla's late ship assignment, side plan, spawn and team crewing for the AI fleet, and latches the attempt.</summary>
public class NavalNpcFleetSpawner : INavalNpcFleetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalNpcFleetSpawner>();

    // AssignShipsToFormations (DefaultNavalMissionLogic) uses the first eight formations.
    private const int MaxShipFormations = 8;

    internal const string WaitingForHost = "waiting_for_host";
    internal const string WaitingForDeployment = "waiting_for_deployment";
    internal const string WaitingForReserve = "waiting_for_reserve";
    internal const string Ready = "ready";
    internal const string Fielded = "fielded";
    internal const string Failed = "failed";

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly BattleSideEnum enemySide;

    // The battle's one attempt, made here or by an earlier host; once set, this client never fields the fleet.
    private NetworkNpcFleetAttempted attempt;
    private bool attemptedHere;
    private string state = WaitingForHost;
    private int hullsSpawned;
    private int troopsFielded;
    private string failure;

    public NavalNpcFleetSpawner(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleShipReplicator shipReplicator,
        ICoopShipSnapshotBuilder snapshotBuilder,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        BattleSideEnum enemySide)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.shipReplicator = shipReplicator;
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.enemySide = enemySide;

        messageBroker.Subscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    // Drops the attempt and entrant subscriptions with the mission.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    /// <summary>Whether the fleet can be fielded now: by the battle host, after its deployment (vanilla's reinforcement-time
    /// spawn path), with the NPC reserve in hand.</summary>
    internal static string Decide(bool isLocalHost, bool deploymentFinished, bool reservePopulated, int npcParties)
    {
        if (!isLocalHost) return WaitingForHost;
        if (!deploymentFinished) return WaitingForDeployment;
        if (!reservePopulated || npcParties == 0) return WaitingForReserve;
        return Ready;
    }

    /// <summary>Vanilla's ship count: the deployment limit, the ships, the crew and the ship formations.</summary>
    internal static int ShipCount(int deploymentLimit, int ships, int troops, int freeFormations) =>
        Math.Max(0, Math.Min(Math.Min(deploymentLimit, ships), Math.Min(troops, Math.Min(freeFormations, MaxShipFormations))));

    // Fields the fleet on the first tick Decide allows, then latches the attempt and sends it to the peers.
    public void Tick()
    {
        var mission = Mission.Current;
        if (HasAttempt || mission == null) return;

        var supplier = EnemySupplier();
        var parties = NpcParties(supplier);
        state = Decide(session.IsLocalHost, mission.IsDeploymentFinished, supplier?.IsPopulated == true, parties.Count);
        if (state != Ready) return;

        // One attempt per battle: it consumes reserve troops, so a failure is reported once instead of retried every tick.
        try
        {
            SpawnFleet(mission, supplier, parties);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            Logger.Error(exception, "[NavalBattle] Could not field the AI fleet of {MapEventId}", session.InstanceId);
        }

        attemptedHere = true;
        Settle(new NetworkNpcFleetAttempted(enemySide, Outcome(hullsSpawned, failure) == Failed));
        network.SendAll(attempt);
    }

    /// <summary>Whether the battle's attempt was made, here or by an earlier host.</summary>
    internal bool HasAttempt => attempt != null;

    // Latches the battle's attempt; its outcome is the spawner's state from then on.
    private void Settle(NetworkNpcFleetAttempted attempted)
    {
        attempt = attempted;
        state = attempted.Failed ? Failed : Fielded;
    }

    private void Handle_NetworkNpcFleetAttempted(MessagePayload<NetworkNpcFleetAttempted> payload)
    {
        var attempted = payload.What;
        GameThread.RunSafe(() => AcceptAttempt(attempted), context: nameof(Handle_NetworkNpcFleetAttempted));
    }

    // [Game thread] An earlier host's attempt: a promoted client doesn't field again, and a failed side releases its hold.
    internal void AcceptAttempt(NetworkNpcFleetAttempted attempted)
    {
        if (HasAttempt) return;

        Settle(attempted);
        Logger.Information("[NavalBattle] The {Side} AI fleet of {MapEventId} was already attempted by a battle host: {Outcome}",
            attempted.Side, session.InstanceId, state);
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() => SendAttemptTo(entered.ControllerId), context: nameof(Handle_PeerEntered));
    }

    // [Game thread] The battle host passes the attempt to an entrant, which a later host change could promote.
    internal void SendAttemptTo(string controllerId)
    {
        if (!HasAttempt || !session.IsLocalHost || session.IsOwn(controllerId)) return;

        network.Send(controllerId, attempt);
    }

    // A failure after some hulls spawned still fails: those hulls have no crew, so the side would never read as fielded.
    internal static string Outcome(int hullsSpawned, string failure) => hullsSpawned > 0 && failure == null ? Fielded : Failed;

    // The attempt's side rather than this client's enemy side: in PvP the battle host's enemy is the other side's own.
    public BattleSideEnum FailedSide => attempt?.Failed == true ? attempt.Side : BattleSideEnum.None;

    // Picks the ships, crew and formations the way vanilla's naval start does, then spawns and crews them; a step that can't
    // go on leaves its reason in failure.
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

        AssignAndSpawnHulls(shipsLogic, navalLogic, planning, team, formations.Take(count).ToList(), ships, parties);
        if (hullsSpawned == 0)
        {
            failure = "no hull spawned";
            return;
        }

        CrewFleet(agentsLogic, team, troops);
    }

    // Assigns a ship to each formation, makes the side's late plan and spawns the hulls; an assignment left without a hull
    // is cleared.
    private void AssignAndSpawnHulls(NavalShipsLogic shipsLogic, DefaultNavalMissionLogic navalLogic,
        NavalMissionDeploymentPlanningLogic planning, Team team, List<Formation> formations,
        List<(Ship snapshot, PartyBase owner)> ships, MBList<MapEventParty> parties)
    {
        for (int index = 0; index < formations.Count; index++)
            shipsLogic.SetShipAssignment(team.TeamSide, formations[index].FormationIndex, ships[index].snapshot);

        try
        {
            // The enemy side was planned with no ships at deployment start, so its plan is made now, after the fact.
            NavalLateDeploymentPlan.MakeForSide(navalLogic, enemySide);
            SpawnAssignedHulls(shipsLogic, planning, team, formations, ships, parties);
        }
        finally
        {
            // An assignment without a hull would count as a ship in every later plan and spawn query.
            foreach (var formation in formations)
            {
                if (shipsLogic.GetShipAssignment(team.TeamSide, formation.FormationIndex).MissionShip == null)
                    shipsLogic.ClearShipAssignment(team.TeamSide, (int)formation.FormationIndex);
            }
        }
    }

    // The naval spawn logic's team crewing (AllocateAndDeployInitialTroopsOfTeam), with the late origins.
    private void CrewFleet(NavalAgentsLogic agentsLogic, Team team, MBList<IAgentOriginBase> troops)
    {
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

    // Spawns each assigned formation's hull at its plan frame, under AI control, and registers it for the peers.
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

    // The enemy side's supplier of this battle; it holds the NPC reserve once the server sent it to the battle host.
    private CoopTroopSupplier EnemySupplier() =>
        CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId).FirstOrDefault(supplier => supplier.Side == enemySide);

    // The reserve's parties with troops left that no player owns, by the player registry the server's fate rule reads.
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

    public object Inspect() => new
    {
        enemySide = enemySide.ToString(),
        state,
        attemptedSide = attempt?.Side.ToString(),
        attemptedHere,
        hullsSpawned,
        firstCrewBatch = troopsFielded,
        failure,
    };
}
