using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
#endif
using GameInterface.Services.Ships.Messages;
using Missions.Battles;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Runs one side's AI (NPC) fleet attempt once, on the battle host through the fielder, late because the NPC reserve
/// reaches the elected host after the mission loaded; every client keeps the attempt's outcome for that side.
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

/// <summary>
/// Runs one side's AI fleet attempt protocol: decides when the attempt may run, latches its outcome, tells the peers and late
/// entrants, and takes back a failed attempt; the fielding itself is the fielder's.
/// </summary>
public class NavalNpcFleetSpawner : INavalNpcFleetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalNpcFleetSpawner>();

    internal const string WaitingForHost = "waiting_for_host";
    internal const string WaitingForDeployment = "waiting_for_deployment";
    internal const string WaitingForReserve = "waiting_for_reserve";
    internal const string Ready = "ready";
    internal const string Fielded = "fielded";
    internal const string Failed = "failed";
    internal const string TakingBack = "taking_back";

    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly INavalNpcFleetFielder fielder;
    private readonly INavalNpcFleetRollback rollback;

    // The side's one attempt, made here or by an earlier host; once set, this client never fields the fleet.
    private NetworkNpcFleetAttempted attempt;
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
#endif
    private string state = WaitingForHost;
    private int hullsRetracted;
    // The attempt made here, with what it did; null until this client attempts.
    private NavalNpcFleetAttemptWork lastAttempt;
    // A failed attempt whose take-back threw; it is retried every tick and the attempt settles only once it went through.
    private bool takeBackPending;
    private int takeBackFailures;
    private string failure;

    // Takes the battle's mesh, the server relay, the session and ship replicator, the side it fields, and builds that battle's
    // fielder and take-back.
    public NavalNpcFleetSpawner(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleShipReplicator shipReplicator,
        BattleSideEnum side,
        Func<IBattleSession, IBattleShipReplicator, INavalNpcFleetFielder> fielders,
        Func<IBattleShipReplicator, INavalNpcFleetRollback> rollbacks
#if DEBUG
        , INavalScenarioState scenarioState
#endif
        )
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.session = session;
        Side = side;
        fielder = fielders(session, shipReplicator);
        rollback = rollbacks(shipReplicator);
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

    // Fields the fleet on the first tick Decide allows, then latches the attempt and sends it to the peers; a failed attempt's
    // unfinished take-back is retried first.
    public void Tick()
    {
        if (takeBackPending)
        {
            FinishFailedAttempt();
            return;
        }

        var mission = Mission.Current;
        if (HasAttempt || mission == null) return;

        var reserve = fielder.ReadReserve(Side);
        state = Decide(session.IsLocalHost, mission.IsDeploymentFinished, reserve.IsPopulated, reserve.NpcParties);
        if (state != Ready) return;

        AttemptFleet(mission, reserve);
    }

    // Makes the side's single eligible spawn attempt through the fielder, including a DEBUG-injected failure, and settles it; a
    // failed attempt is taken back first, so peers drop its hulls before they hear it failed.
    internal void AttemptFleet(Mission mission, NavalNpcReserve reserve)
    {
        if (HasAttempt || lastAttempt != null) return;

        // One attempt per side: it consumes reserve troops, so a failure is reported once instead of retried every tick.
        lastAttempt = new NavalNpcFleetAttemptWork();
        try
        {
#if DEBUG
            lastAttempt.ArmedFailure = ScenarioFailsSpawn() ? NavalFleetFailStage.BeforeSpawn : failNextSpawn;
            failNextSpawn = null;
            lastAttempt.ThrowIfArmed(NavalFleetFailStage.BeforeSpawn);
#endif
            failure = fielder.Field(mission, Side, reserve, lastAttempt);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            Logger.Error(exception, "[NavalBattle] Could not field the {Side} AI fleet of {MapEventId}", Side, session.InstanceId);
        }

        if (Outcome(lastAttempt.Hulls.Count, failure) == Failed)
        {
            FinishFailedAttempt();
            return;
        }

        Publish(failed: false);
    }

    // Takes back the failed attempt and then settles it as failed; a take-back that throws is retried on the next tick.
    private void FinishFailedAttempt()
    {
        takeBackPending = !TryTakeBack(lastAttempt);
        if (takeBackPending)
        {
            state = TakingBack;
            return;
        }

        hullsRetracted = lastAttempt.Hulls.Count;
        if (HasAttempt) return;

        LogNotFielded();
        Publish(failed: true);
    }

    // Players taking all of the side's ships is a normal outcome, so only a real failure is a warning.
    private void LogNotFielded()
    {
        if (failure == NavalNpcFleetFielder.NoShipsLeft)
        {
            Logger.Information("[NavalBattle] The {Side} side of {MapEventId} fields no AI fleet: {Reason}", Side, session.InstanceId,
                failure);
            return;
        }

        Logger.Warning("[NavalBattle] The {Side} AI fleet of {MapEventId} was not fielded ({Failure}); {Hulls} hull(s) taken back",
            Side, session.InstanceId, failure, hullsRetracted);
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

    // Latches this host's attempt, with the hulls it left and the players its share counted, and tells the peers and the server,
    // which passes it on to every client for a later joiner's cap.
    private void Publish(bool failed)
    {
        var players = lastAttempt.Players;
        Settle(new NetworkNpcFleetAttempted(session.InstanceId, Side, failed, failed ? 0 : lastAttempt.Hulls.Count, players.Hulls,
            players.PartyIds));
        network.SendAll(attempt);
        relayNetwork.SendAll(attempt);
    }

    /// <summary>Whether the side's attempt was made, here or by an earlier host.</summary>
    internal bool HasAttempt => attempt != null;

    // Latches the side's attempt; its outcome is the spawner's state from then on.
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
    // its hold. The other side's attempt belongs to that side's spawner, and the server passes on other battles' too.
    internal void AcceptAttempt(NetworkNpcFleetAttempted attempted)
    {
        if (HasAttempt || attempted.Side != Side || attempted.InstanceId != session.InstanceId) return;

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

    // The spawner's state and the side's attempt, for the naval inspect command.
    public object Inspect() => new
    {
        side = Side.ToString(),
        state,
        attemptedHere = lastAttempt != null,
        hullsSpawned = lastAttempt?.Hulls.Count ?? 0,
        hullsRetracted,
        firstCrewBatch = lastAttempt?.FirstCrewBatch ?? 0,
        failure,
    };
}
