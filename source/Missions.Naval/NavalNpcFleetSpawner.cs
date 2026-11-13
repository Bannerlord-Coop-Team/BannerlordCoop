using Common;
using Common.Logging;
using Common.Messaging;
#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
#endif
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
/// [Game thread] Fields one side's AI (NPC) fleet late, once per battle and on the battle host only, because the NPC reserve
/// reaches the elected host after the mission loaded. Every client keeps the attempt's outcome for that side.
/// </summary>
public interface INavalNpcFleetSpawner : IDisposable
{
    /// <summary>[Game thread] Spawns the fleet once its conditions hold.</summary>
    void Tick();
#if DEBUG
    /// <summary>Arms a failure at <paramref name="stage"/> of this battle host's next eligible spawn attempt; returns a refusal or null.</summary>
    string FailNextSpawn(NavalFleetFailStage stage = NavalFleetFailStage.BeforeSpawn);
#endif

    /// <summary>The battle side whose AI parties this spawner fields.</summary>
    BattleSideEnum Side { get; }

    /// <summary>Whether the side's fleet was attempted, here or by an earlier host, and could not be fielded.</summary>
    bool AttemptFailed { get; }

    /// <summary>Spawner state (diagnostics).</summary>
    object Inspect();
}

#if DEBUG
/// <summary>Where a DEBUG-armed fleet failure throws inside the attempt.</summary>
public enum NavalFleetFailStage
{
    /// <summary>Before anything spawns or is pulled.</summary>
    BeforeSpawn,

    /// <summary>Right after the first hull spawned and registered.</summary>
    AfterFirstHull,

    /// <summary>Inside the crewing, after its first batch spawned.</summary>
    DuringCrewing,
}
#endif

/// <summary>Runs vanilla's late ship assignment, side plan, spawn and team crewing for one side's AI fleet, and latches the attempt.</summary>
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
    internal const string TakingBack = "taking_back";

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INavalLateDeploymentPlanTracker latePlans;
    private readonly INavalNpcFleetRollback rollback;
    private readonly IBattleTeamResolver teamResolver;
    private readonly INavalShipBudget shipBudget;

    // The side's one attempt, made here or by an earlier host; once set, this client never fields the fleet.
    private NetworkNpcFleetAttempted attempt;
    private bool attemptedHere;
#if DEBUG
    private NavalFleetFailStage? failNextSpawn;
    private readonly INavalScenarioState scenarioState;

    // Read at the eligible attempt, since the scenario can arrive after the mission opens or the host is elected.
    private bool ScenarioFailsSpawn() =>
        scenarioState.Current is { FailNpcFleet: true } scenario && scenario.MapEventId == session.InstanceId;

    // The battle host owns AI fielding, so this debug mutation runs there rather than on the campaign server.
    public string FailNextSpawn(NavalFleetFailStage stage = NavalFleetFailStage.BeforeSpawn)
    {
        if (!session.IsLocalHost) return "Run fleet_fail on the battle host client.";
        if (HasAttempt) return $"The {Side} AI fleet was already attempted in this battle.";
        failNextSpawn = stage;
        return null;
    }

    // Throws the armed failure once the attempt reaches its stage, through the attempt's normal failure path.
    private void ThrowIfArmed(NavalFleetFailStage stage)
    {
        if (failNextSpawn != stage) return;

        failNextSpawn = null;
        throw new InvalidOperationException($"DEBUG fleet_fail: forced AI fleet spawn failure ({stage})");
    }
#endif
    private string state = WaitingForHost;
    private int hullsSpawned;
    private int hullsRetracted;
    // A failed attempt whose take-back threw; it is retried every tick and the attempt settles only once it went through.
    private NavalNpcFleetAttemptWork unfinishedTakeBack;
    private int takeBackFailures;
    private int troopsFielded;
    private string failure;

    // Takes the battle's mesh, session and ship replicator, the vanilla spawn helpers and the side whose AI parties it fields.
    public NavalNpcFleetSpawner(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleShipReplicator shipReplicator,
        ICoopShipSnapshotBuilder snapshotBuilder,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INavalLateDeploymentPlanTracker latePlans,
        INavalNpcFleetRollback rollback,
        IBattleTeamResolver teamResolver,
        INavalShipBudget shipBudget,
        BattleSideEnum side
#if DEBUG
        , INavalScenarioState scenarioState
#endif
        )
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.shipReplicator = shipReplicator;
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.latePlans = latePlans;
        this.rollback = rollback;
        this.teamResolver = teamResolver;
        this.shipBudget = shipBudget;
        Side = side;
#if DEBUG
        this.scenarioState = scenarioState;
#endif

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

    /// <summary>Vanilla's ship count before crewing: the deployment limit, the ships and the free ship formations.</summary>
    internal static int ShipCount(int deploymentLimit, int ships, int freeFormations) =>
        Math.Max(0, Math.Min(Math.Min(deploymentLimit, ships), Math.Min(freeFormations, MaxShipFormations)));

    // Fields the fleet on the first tick Decide allows, then latches the attempt and sends it to the peers; a failed attempt's
    // unfinished take-back is retried first.
    public void Tick()
    {
        if (unfinishedTakeBack != null)
        {
            FinishFailedAttempt(unfinishedTakeBack);
            return;
        }

        var mission = Mission.Current;
        if (HasAttempt || mission == null) return;

        var supplier = SideSupplier();
        var parties = NpcParties(supplier);
        state = Decide(session.IsLocalHost, mission.IsDeploymentFinished, supplier?.IsPopulated == true, parties.Count);
        if (state != Ready) return;

        AttemptFleet(work => SpawnFleet(mission, supplier, parties, work));
    }

    // Makes the single eligible spawn attempt, including a DEBUG-injected failure, and settles it; a failed attempt is taken
    // back first, so peers drop its hulls before they hear it failed.
    internal void AttemptFleet(Func<NavalNpcFleetAttemptWork, string> spawn)
    {
        if (HasAttempt || unfinishedTakeBack != null) return;

        // One attempt per battle: it consumes reserve troops, so a failure is reported once instead of retried every tick.
        var work = new NavalNpcFleetAttemptWork();
        try
        {
#if DEBUG
            if (ScenarioFailsSpawn()) failNextSpawn = NavalFleetFailStage.BeforeSpawn;
            ThrowIfArmed(NavalFleetFailStage.BeforeSpawn);
#endif
            failure = spawn(work);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            Logger.Error(exception, "[NavalBattle] Could not field the {Side} AI fleet of {MapEventId}", Side, session.InstanceId);
        }

        attemptedHere = true;
        hullsSpawned = work.Hulls.Count;
        if (Outcome(hullsSpawned, failure) == Failed)
        {
            FinishFailedAttempt(work);
            return;
        }

        Publish(new NetworkNpcFleetAttempted(Side, failed: false));
    }

    // Takes back a failed attempt and then settles it as failed; a take-back that throws keeps the work for the next tick.
    private void FinishFailedAttempt(NavalNpcFleetAttemptWork work)
    {
        if (!TryTakeBack(work))
        {
            unfinishedTakeBack = work;
            state = TakingBack;
            return;
        }

        unfinishedTakeBack = null;
        hullsRetracted = work.Hulls.Count;
        if (HasAttempt) return;

        Logger.Warning("[NavalBattle] The {Side} AI fleet of {MapEventId} was not fielded ({Failure}); {Hulls} hull(s) taken back",
            Side, session.InstanceId, failure, hullsRetracted);
        Publish(new NetworkNpcFleetAttempted(Side, failed: true));
    }

    // The first throw is logged with its exception and a later recovery with the tries it took, so a stuck take-back logs once.
    private bool TryTakeBack(NavalNpcFleetAttemptWork work)
    {
        try
        {
            rollback.Rollback(work);
        }
        catch (Exception exception)
        {
            if (++takeBackFailures == 1)
                Logger.Error(exception,
                    "[NavalBattle] Could not take back the failed {Side} AI fleet attempt of {MapEventId} ({Hulls} hull(s)); retrying every tick",
                    Side, session.InstanceId, work.Hulls.Count);
            return false;
        }

        if (takeBackFailures > 0)
            Logger.Information("[NavalBattle] Took back the failed {Side} AI fleet attempt of {MapEventId} after {Tries} tries",
                Side, session.InstanceId, takeBackFailures + 1);
        return true;
    }

    // Latches this host's attempt and tells the peers.
    private void Publish(NetworkNpcFleetAttempted attempted)
    {
        Settle(attempted);
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

    // [Poller] Hands an earlier host's attempt to the game thread, where the spawner's state lives.
    private void Handle_NetworkNpcFleetAttempted(MessagePayload<NetworkNpcFleetAttempted> payload)
    {
        var attempted = payload.What;
        GameThread.RunSafe(() => AcceptAttempt(attempted), context: nameof(Handle_NetworkNpcFleetAttempted));
    }

    // [Game thread] An earlier host's attempt of this side: a promoted client doesn't field it again, and a failed side releases
    // its hold. The other side's attempt belongs to that side's spawner.
    internal void AcceptAttempt(NetworkNpcFleetAttempted attempted)
    {
        if (HasAttempt || attempted.Side != Side) return;

        Settle(attempted);
        Logger.Information("[NavalBattle] The {Side} AI fleet of {MapEventId} was already attempted by a battle host: {Outcome}",
            attempted.Side, session.InstanceId, state);
    }

    // [Poller] Hands an entrant of this battle to the game thread, which sends it the attempt.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() => SendAttemptTo(entered.ControllerId), context: nameof(Handle_PeerEntered));
    }

    // [Game thread] Every client holding the attempt passes it to an entrant, so one entering during a host change still gets it.
    internal void SendAttemptTo(string controllerId)
    {
        if (!HasAttempt || session.IsOwn(controllerId)) return;

        network.Send(controllerId, attempt);
    }

    // A failure after some hulls spawned still fails: the attempt takes them back, so the side never reads as fielded.
    internal static string Outcome(int hullsSpawned, string failure) => hullsSpawned > 0 && failure == null ? Fielded : Failed;

    public BattleSideEnum Side { get; }

    public bool AttemptFailed => attempt?.Failed == true;

    // Picks the ships, crew and formations the way vanilla's naval start does, then spawns and crews them; returns why a step
    // couldn't go on, or null once the fleet is crewed.
    private string SpawnFleet(Mission mission, CoopTroopSupplier supplier, MBList<MapEventParty> parties, NavalNpcFleetAttemptWork work)
    {
        var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
        var agentsLogic = mission.GetMissionBehavior<NavalAgentsLogic>();
        var navalLogic = mission.GetMissionBehavior<DefaultNavalMissionLogic>();
        var planning = mission.GetMissionBehavior<NavalMissionDeploymentPlanningLogic>();
        if (shipsLogic == null || agentsLogic == null || navalLogic == null || planning == null)
            return "naval mission behaviors missing";

        // The team every peer puts this side's AI hulls on, so the host's own allies go on its ally team.
        var team = teamResolver.ResolveAiFleetTeam(Side);
        if (team == null) return "no team for the side's AI hulls";

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent)) return "map event not found";

        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        var limit = shipBudget.AiLimit(mapEvent.GetMapEventSide(Side).Parties, parties);
        var campaignShips = new MBList<IShipOrigin>();
        model.FillShipsOfTeamParties(parties, limit, campaignShips);
        var ships = campaignShips.OfType<Ship>().Select(ship => (snapshot: snapshotBuilder.Build(ship), owner: ship.Owner)).ToList();
        var formations = team.FormationsIncludingEmpty
            .Where(formation => (int)formation.FormationIndex < MaxShipFormations
                && !shipsLogic.GetShipAssignment(team.TeamSide, formation.FormationIndex).IsSet)
            .ToList();

        // Crew only for the hulls that can field, so troops past their capacity stay in the reserve instead of being pulled.
        int planned = ShipCount(limit.NetDeploymentLimit, ships.Count, formations.Count);
        var plannedShips = ships.Take(planned).Select(ship => (IShipOrigin)ship.snapshot).ToMBList();
        var troops = PullTroops(supplier, parties, model.GetMaximumDeployableTroopCountForTeam(plannedShips, isPlayerTeam: false));
        work.Troops.AddRange(troops);
        int count = Math.Min(planned, troops.Count);
        Logger.Information("[NavalBattle] Fielding the AI fleet of {MapEventId}: side={Side} team={Team} parties={Parties} ships={Ships} " +
            "limit={Limit} troops={Troops} freeFormations={Formations} count={Count}",
            session.InstanceId, Side, team.TeamSide, parties.Count, ships.Count, limit.NetDeploymentLimit, troops.Count, formations.Count,
            count);
        if (count == 0) return "no ship to field";

        AssignAndSpawnHulls(shipsLogic, navalLogic, planning, team, formations.Take(count).ToList(), ships, parties, work);
        if (work.Hulls.Count == 0) return "no hull spawned";

        CrewFleet(agentsLogic, team, troops, work);
        return null;
    }

    // Assigns a ship to each formation, makes the side's late plan and spawns the hulls; an assignment left without a hull
    // is cleared.
    private void AssignAndSpawnHulls(NavalShipsLogic shipsLogic, DefaultNavalMissionLogic navalLogic,
        NavalMissionDeploymentPlanningLogic planning, Team team, List<Formation> formations,
        List<(Ship snapshot, PartyBase owner)> ships, MBList<MapEventParty> parties, NavalNpcFleetAttemptWork work)
    {
        for (int index = 0; index < formations.Count; index++)
            shipsLogic.SetShipAssignment(team.TeamSide, formations[index].FormationIndex, ships[index].snapshot);

        try
        {
            // The AI team was planned with no ships at deployment start, so its plan is made now, after the fact. Vanilla doesn't
            // redo a team that already has a plan (MakeDeploymentPlan), so on the host's own side this only plans the ally team.
            latePlans.MakeForSide(navalLogic, Side);
            SpawnAssignedHulls(shipsLogic, planning, team, formations, ships, parties, work);
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
    private void CrewFleet(NavalAgentsLogic agentsLogic, Team team, MBList<IAgentOriginBase> troops, NavalNpcFleetAttemptWork work)
    {
        var teamSide = team.TeamSide;
        work.BeginCrewing(teamSide, agentsLogic.GetSpawnReinforcementsOnTick(teamSide));
        agentsLogic.AddTroopOrigins(teamSide, troops);
        agentsLogic.AutoComputeDesiredTroopCountsPerShip(teamSide);
        agentsLogic.AssignTroops(teamSide);
        agentsLogic.InitializeReinforcementTimers(teamSide);
        agentsLogic.SetSpawnReinforcementsOnTick(teamSide, true);
        troopsFielded = agentsLogic.SpawnNextBatch(teamSide);
#if DEBUG
        ThrowIfArmed(NavalFleetFailStage.DuringCrewing);
#endif
        agentsLogic.AssignAndTeleportCrewToShipMachines(teamSide);
        Logger.Information("[NavalBattle] Fielded {Hulls} AI hull(s) and a first crew batch of {Troops} on {Team}",
            work.Hulls.Count, troopsFielded, team.TeamSide);
    }

    // Spawns each assigned formation's hull at its plan frame, under AI control, and registers it for the peers. Each hull is
    // recorded as soon as it exists, so a later throw still takes it back.
    private void SpawnAssignedHulls(NavalShipsLogic shipsLogic, NavalMissionDeploymentPlanningLogic planning, Team team,
        List<Formation> formations, List<(Ship snapshot, PartyBase owner)> ships, MBList<MapEventParty> parties,
        NavalNpcFleetAttemptWork work)
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

            work.Hulls.Add(hull);
            hull.SetController(NavalDLC.Missions.ShipControl.ShipControllerType.AI);
            formation.SetControlledByAI(true);
            shipReplicator.RegisterNpcHull(hull, formation, PartyIdOf(parties, ships[index].owner));
#if DEBUG
            ThrowIfArmed(NavalFleetFailStage.AfterFirstHull);
#endif
        }
    }

    // The side's supplier of this battle; it holds the NPC reserve once the server sent it to the battle host, next to the host's
    // own party on the host's side.
    private CoopTroopSupplier SideSupplier() =>
        CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId).FirstOrDefault(supplier => supplier.Side == Side);

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

    // The spawner's state and the battle's attempt, for the naval inspect command.
    public object Inspect() => new
    {
        side = Side.ToString(),
        state,
        attemptedHere,
        hullsSpawned,
        hullsRetracted,
        firstCrewBatch = troopsFielded,
        failure,
    };
}
