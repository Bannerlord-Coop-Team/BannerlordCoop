using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>The agents this client seated at a ship station, whose movement and actions the handlers leave to the station.</summary>
public interface IShipStationSeats
{
    /// <summary>[Game thread] Whether this client seated the agent.</summary>
    bool IsOwnSeat(Guid agentId);
}

/// <summary>
/// Replicates ship station use: the owner of an agent sends every use and release of a hull's usable point, and peers
/// seat or release their puppet on the same point of their hull. Agents seated by their owner send no world movement.
/// </summary>
public interface IAgentStationUseReplicator : IShipStationSeats, IDisposable
{
    /// <summary>[Mission tick] Applies received uses, announces own seats at the deployment commit and to joiners, and re-pins
    /// applied seats.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] The station an agent uses on a registered hull, or null (diagnostics).</summary>
    string DescribeStation(Agent agent);

    /// <summary>
    /// [Game thread] Lets every puppet this client seated on <paramref name="hull"/> go before the hull leaves the mission, so no
    /// machine of a removed hull keeps a user; how many it released.
    /// </summary>
    int ReleaseSeatsOn(MissionObject hull);

    /// <summary>[Game thread] The stream's counters and collection sizes (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IAgentStationUseReplicator"/>
public class AgentStationUseReplicator : IAgentStationUseReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentStationUseReplicator>();

    // A use for an agent or hull that never arrives is dropped after this long.
    internal const float PendingLifetimeSeconds = 10f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IMissionContext missionContext;

    private readonly Dictionary<Guid, long> sentRevisions = new Dictionary<Guid, long>();
    private readonly Dictionary<Guid, NetworkAgentStationUse> ownSeated = new Dictionary<Guid, NetworkAgentStationUse>();
    private readonly Dictionary<Guid, AuthorityStamp> appliedStamps = new Dictionary<Guid, AuthorityStamp>();
    private readonly List<PendingUse> pending = new List<PendingUse>();
    private readonly Dictionary<Guid, AppliedSeat> appliedSeats = new Dictionary<Guid, AppliedSeat>();
    private readonly List<string> joinersAwaitingSeats = new List<string>();
    // Reused by the per-tick passes, so a battle with seated crew allocates no list per frame.
    private readonly List<Guid> scratchAgentIds = new List<Guid>();
    private float elapsed;
    // While this client deploys, copies are held back, so their crew's uses wait without aging.
    private float deploymentHeldAt;
    private bool seatsAnnounced;
    // A game-thread closure queued before Dispose still runs; it must not send or seat for a torn-down battle.
    private bool disposed;
    // dropped counts uses that were lost and unownedPuppetReleases seats vanilla took from a puppet its owner keeps seated, less
    // those the owner's late release showed were its own; staleUses and obsoleteReleases (nothing left to release) are expected.
    private long sent, applied, dropped, staleUses, obsoleteReleases, unownedPuppetReleases, pinned, ownerReleases,
        sinkingUseReleases, sinkingSafetyNetReleases, withdrawnReleases, removedHullSeats, departedHullReleases, detachedSeats,
        adoptedSeats;

    // Subscribes to this client's use and release patches, the owners' uses and mission entries, and gives the movement and
    // action handlers its seats.
    public AgentStationUseReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        ICoopMissionComponent missionComponent,
        INavalShipEngine engine,
        IBattleDeploymentCoordinator deployment,
        IMissionContext missionContext)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.deployment = deployment;
        this.missionContext = missionContext;

        messageBroker.Subscribe<AgentStationUseChanged>(Handle_AgentStationUseChanged);
        messageBroker.Subscribe<NetworkAgentStationUse>(Handle_NetworkAgentStationUse);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        missionComponent.AgentMovementHandler.ConfigureStationSeats(this);
        missionComponent.AgentActionHandler.ConfigureStationSeats(this);
    }

    // Stops the subscriptions and takes the seats off the handlers; a closure queued before this does nothing.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<AgentStationUseChanged>(Handle_AgentStationUseChanged);
        messageBroker.Unsubscribe<NetworkAgentStationUse>(Handle_NetworkAgentStationUse);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        missionComponent.AgentMovementHandler.ConfigureStationSeats(null);
        missionComponent.AgentActionHandler.ConfigureStationSeats(null);
    }

    // [Any thread] Published by the use/release patches, which also run on an agent tick worker; the seats and revisions it
    // updates live on the game thread, so it is announced there (inline when already on it).
    private void Handle_AgentStationUseChanged(MessagePayload<AgentStationUseChanged> payload)
    {
        var change = payload.What;
        GameThread.RunSafe(() => AnnounceStationUse(change), context: nameof(Handle_AgentStationUseChanged));
    }

    // [Game thread] Sends a station use or release of an agent this client drives.
    private void AnnounceStationUse(AgentStationUseChanged change)
    {
        if (disposed) return;

        // A puppet's seat is its owner's to announce. Vanilla also releases puppets here for the reasons it releases the owner's
        // agent (a ramp over an oar, a death, a sinking), so a local release is judged by the refresh, after the owner's arrived.
        var agents = missionComponent.AgentRegistry;
        if (change.Agent == null || !agents.TryGetAgentInfo(change.Agent, out var info) || !agents.IsLocallyControlled(change.Agent))
            return;

        // A use deferred from a tick worker can come after a later release went out inline; the agent's state decides.
        if (change.InUse != engine.IsSeated(change.Agent, change.Point)) return;

        if (!engine.TryDescribeStation(change.Point, out var hull, out var stationKey, out int pointIndex)
            || !missionComponent.ShipRegistry.TryGetByHull(hull, out var ship))
            return;

        sentRevisions.TryGetValue(info.AgentId, out long revision);
        var use = new NetworkAgentStationUse(info.AgentId, ship.ShipId, stationKey, pointIndex, change.InUse, revision + 1,
            session.OwnControllerId);
        sentRevisions[info.AgentId] = use.Revision;
        if (use.InUse) ownSeated[info.AgentId] = use;
        else ownSeated.Remove(info.AgentId);

        // Before the deployment commit peers have no own crew yet; the commit announces the seats taken by then.
        if (!seatsAnnounced) return;

        network.SendAll(use);
        sent++;
    }

    public bool IsOwnSeat(Guid agentId) => ownSeated.ContainsKey(agentId);

    // [Poller thread] Hands an owner's use to the game thread, which keeps it pending.
    private void Handle_NetworkAgentStationUse(MessagePayload<NetworkAgentStationUse> payload)
    {
        var use = payload.What;
        GameThread.RunSafe(() => AcceptUse(use), context: nameof(Handle_NetworkAgentStationUse));
    }

    /// <summary>
    /// [Game thread] Keeps an owner's station use pending; the next tick applies it, once its agent and hull are here, as seating
    /// from the game-thread queue would overlap the engine's async agent tick.
    /// </summary>
    internal void AcceptUse(NetworkAgentStationUse use)
    {
        if (disposed || use == null) return;
        pending.Add(new PendingUse(use, elapsed));
    }

    public void Tick(float dt)
    {
        elapsed += Math.Max(0f, dt);
        if (engine.IsDeploymentMode) deploymentHeldAt = elapsed;
        PruneOwnSeats();
        SendSeatsToJoiners();
        if (!seatsAnnounced && deployment.IsCommitted)
        {
            seatsAnnounced = true;
            foreach (var use in ownSeated.Values)
                network.SendAll(use);
            sent += ownSeated.Count;
        }

        DrainPending();
        RefreshAppliedSeats();
    }

    // An agent whose authority moved away sends no release, so its seat is no longer this client's to announce or withhold.
    private void PruneOwnSeats()
    {
        if (ownSeated.Count == 0) return;

        var agents = missionComponent.AgentRegistry;
        scratchAgentIds.Clear();
        foreach (var agentId in ownSeated.Keys)
            if (!agents.IsLocallyControlled(agentId)) scratchAgentIds.Add(agentId);
        foreach (var agentId in scratchAgentIds)
            ownSeated.Remove(agentId);
    }

    /// <summary>An applied seat lasts while its puppet lives, stays registered and remote, and its hull is afloat and in the
    /// mission.</summary>
    internal static bool KeepsAppliedSeat(bool alive, bool registered, bool ownedHere, bool hullSinking, bool hullRemoved) =>
        alive && registered && !ownedHere && !hullSinking && !hullRemoved;

    // [Game thread] Runs after this frame's hull frame writes and the owners' uses. A puppet the owner keeps seated is re-pinned
    // to its station's user frame on the moved hull; if vanilla released it locally it is re-seated once per owner revision.
    internal void RefreshAppliedSeats()
    {
        if (appliedSeats.Count == 0) return;

        var leftAgents = scratchAgentIds;
        leftAgents.Clear();
        foreach (var entry in appliedSeats)
        {
            var seat = entry.Value;
            bool alive = engine.IsAlive(seat.Agent);
            bool registered = missionComponent.AgentRegistry.TryGetAgentInfo(entry.Key, out var info);
            bool ownedHere = registered && info.CurrentAuthority == session.OwnControllerId;
            bool hullSinking = engine.IsSinking(seat.Hull);
            bool hullRemoved = engine.IsHullRemoved(seat.Hull);
            if (!KeepsAppliedSeat(alive, registered, ownedHere, hullSinking, hullRemoved))
            {
                leftAgents.Add(entry.Key);
                if (alive && !ownedHere && hullSinking) ReleaseFromSinkingHull(entry.Key, seat);
                else if (alive && !registered && engine.IsSeated(seat.Agent, seat.Point)) ReleaseWithdrawn(entry.Key, seat);
                else if (alive && hullRemoved) ForgetRemovedHullSeat(entry.Key);
                else if (alive && ownedHere && engine.IsSeated(seat.Agent, seat.Point)) AdoptSeat(entry.Key, seat);
                continue;
            }

            if (!engine.IsSeated(seat.Agent, seat.Point))
            {
                if (seat.LeftLocally) continue;
                if (!IsStillStationOf(seat))
                {
                    leftAgents.Add(entry.Key);
                    ForgetDetachedSeat(entry.Key);
                    continue;
                }

                seat.LeftLocally = true;
                unownedPuppetReleases++;
                Logger.Warning("[NavalSync] Puppet {AgentId} left its station {Station} locally while its owner keeps it seated; " +
                    "re-seating it once", entry.Key, DescribeStation(seat.Point));
                Apply(seat.Agent, seat.Point, inUse: true);
                if (!engine.IsSeated(seat.Agent, seat.Point)) continue;
            }

            engine.PinToStation(seat.Agent, seat.Point);
            pinned++;
        }

        foreach (var agentId in leftAgents)
            appliedSeats.Remove(agentId);
    }

    public int ReleaseSeatsOn(MissionObject hull)
    {
        var seated = appliedSeats.Where(entry => ReferenceEquals(entry.Value.Hull, hull)).ToList();
        foreach (var entry in seated)
        {
            appliedSeats.Remove(entry.Key);
            if (engine.IsSeated(entry.Value.Agent, entry.Value.Point)) Apply(entry.Value.Agent, entry.Value.Point, inUse: false);
        }

        departedHullReleases += seated.Count;
        return seated.Count;
    }

    // A point vanilla consumed or detached (a picked-up item, a broken-off machine) is no longer a station of the seat's hull.
    private bool IsStillStationOf(AppliedSeat seat) =>
        engine.TryDescribeStation(seat.Point, out var hull, out _, out _) && ReferenceEquals(hull, seat.Hull);

    // Re-seating onto a point that left its hull would bind the puppet to a freed object.
    private void ForgetDetachedSeat(Guid agentId)
    {
        detachedSeats++;
        Logger.Warning("[NavalSync] Puppet {AgentId}'s station left its hull; no longer seating it there", agentId);
    }

    // Safety net for an owner whose crew drowns seated without a release; normally the owner's releases arrive first.
    private void ReleaseFromSinkingHull(Guid agentId, AppliedSeat seat)
    {
        sinkingSafetyNetReleases++;
        Logger.Information("[NavalSync] Released puppet {AgentId} from a station of a sinking hull", agentId);
        Apply(seat.Agent, seat.Point, inUse: false);
    }

    // Vanilla's own removal moves the crew off a removed or pooled hull; re-seating onto its machines would bind a puppet to them.
    private void ForgetRemovedHullSeat(Guid agentId)
    {
        removedHullSeats++;
        Logger.Warning("[NavalSync] Puppet {AgentId}'s station is on a hull that left the mission; no longer seating it there", agentId);
    }

    // A puppet adopted while seated is this client's from now on: announcing its seat withholds its movement and actions, and
    // peers and joiners get the seat under this client's stamp.
    private void AdoptSeat(Guid agentId, AppliedSeat seat)
    {
        AnnounceStationUse(new AgentStationUseChanged(seat.Agent, seat.Point, inUse: true));
        if (IsOwnSeat(agentId)) adoptedSeats++;
    }

    // Removal in an ended mission skips vanilla's station release, so a withdrawn puppet must not stay a machine's user.
    private void ReleaseWithdrawn(Guid agentId, AppliedSeat seat)
    {
        withdrawnReleases++;
        Logger.Information("[NavalSync] Released withdrawn puppet {AgentId} from its station", agentId);
        Apply(seat.Agent, seat.Point, inUse: false);
    }

    // Settles every pending use it can: applied, dropped with its gone hull, or expired.
    private void DrainPending()
    {
        if (pending.Count == 0) return;
        pending.RemoveAll(entry => TryApply(entry.Use) || DropForGoneHull(entry) || Expire(entry));
    }

    // A hull this client gave up spawning, or one that left the mission, never arrives, and its crew's uses go with it.
    private bool DropForGoneHull(PendingUse entry)
    {
        if (!missionComponent.ShipRegistry.IsGone(entry.Use.ShipId)) return false;

        // The removal already let the crew go here; their releases, sent after the removal, have nothing left to do.
        if (!entry.Use.InUse) return SettleObsoleteRelease(entry.Use, "its hull left the mission");

        dropped++;
        Logger.Warning("[NavalSync] Dropped station use {Revision} of agent {AgentId}: hull {ShipId} could not spawn here or left the mission",
            entry.Use.Revision, entry.Use.AgentId, entry.Use.ShipId);
        return true;
    }

    // A release whose puppet or hull is already gone; its stamp still orders the sender's older uses, so none of them seats later.
    private bool SettleObsoleteRelease(NetworkAgentStationUse use, string reason)
    {
        appliedStamps[use.AgentId] = use.Stamp;
        obsoleteReleases++;
        Logger.Debug("[NavalSync] Settled station release {Revision} of agent {AgentId} on hull {ShipId}: {Reason}",
            use.Revision, use.AgentId, use.ShipId, reason);
        return true;
    }

    // Whether a pending use outlived its lifetime, counted from its receipt or the end of this client's deployment hold.
    internal static bool IsPendingExpired(float elapsed, float receivedAt, float deploymentHeldAt) =>
        elapsed - Math.Max(receivedAt, deploymentHeldAt) >= PendingLifetimeSeconds;

    // Drops and logs a pending use that outlived its lifetime.
    private bool Expire(PendingUse entry)
    {
        if (!IsPendingExpired(elapsed, entry.ReceivedAt, deploymentHeldAt)) return false;

        dropped++;
        Logger.Warning("[NavalSync] Dropped station use {Revision} of agent {AgentId} from {Sender}: agent or hull {ShipId} never arrived, or the sender never owned the agent or joined the mission",
            entry.Use.Revision, entry.Use.AgentId, entry.Use.SenderControllerId, entry.Use.ShipId);
        return true;
    }

    // [Game thread] True when settled (applied or dropped), false to retry while the agent, hull or sender's membership is still
    // missing or this client has not yet moved the agent's authority to the sender (a new owner's uses can beat the migration).
    internal bool TryApply(NetworkAgentStationUse use)
    {
        // Each owner numbers its own uses, so a use from a different sender than the last applied one starts a fresh order.
        appliedStamps.TryGetValue(use.AgentId, out var last);
        if (!use.Stamp.IsNewer(last))
        {
            staleUses++;
            return true;
        }

        // A puppet that died or withdrew has no seat to leave, and one not spawned yet has none to take.
        var agents = missionComponent.AgentRegistry;
        bool registered = agents.TryGetAgentInfo(use.AgentId, out var info) && info.Agent != null;
        if (!registered && !use.InUse) return SettleObsoleteRelease(use, "its puppet is not registered");

        // An agent this client adopted takes no more uses, also not a departed host's last ones still in flight.
        if (registered && agents.IsLocallyControlled(info.Agent))
        {
            staleUses++;
            return true;
        }

        // A relayed seat catch-up can beat the sender's announced entry; a departed sender's uses expire or go with its hulls.
        if (AuthorityStamp.ValidateMember(use.SenderControllerId, missionContext.ControllersInMission) != null) return false;
        if (!registered || !missionComponent.ShipRegistry.TryGet(use.ShipId, out var ship)) return false;

        if (info.CurrentAuthority != use.SenderControllerId) return false;

        if (engine.IsHullRemoved(ship.Hull))
        {
            if (!use.InUse) return SettleObsoleteRelease(use, "its hull left the mission");

            dropped++;
            Logger.Warning("[NavalSync] Dropped station use {Revision} of agent {AgentId}: hull {ShipId} left the mission",
                use.Revision, use.AgentId, use.ShipId);
            return true;
        }

        var point = engine.ResolveStation(ship.Hull, use.StationKey, use.PointIndex);
        if (point == null)
        {
            dropped++;
            Logger.Warning("[NavalSync] Station {Station}#{Point} of hull {ShipId} not found for agent {AgentId}",
                use.StationKey, use.PointIndex, use.ShipId, use.AgentId);
            return true;
        }

        // Vanilla here can release a puppet before its owner's release for the same cause (a ramp over an oar) arrives, and the
        // refresh may have re-seated it meanwhile.
        if (!use.InUse && (CountedUnowned(use.AgentId, point) || !engine.IsSeated(info.Agent, point)))
            return SettleLocalRelease(use, info.Agent, point);

        // A sinking hull seats nobody; a late use from its owner only lets the puppet go.
        bool inUse = use.InUse && !engine.IsSinking(ship.Hull);
        if (!use.InUse) ownerReleases++;
        else if (!inUse) sinkingUseReleases++;
        appliedStamps[use.AgentId] = use.Stamp;
        Apply(info.Agent, point, inUse);
        RecordAppliedSeat(use.AgentId, info.Agent, ship.Hull, inUse ? point : null);
        return true;
    }

    // Whether the refresh counted the puppet's local release from this point as unowned, before the owner's next revision.
    private bool CountedUnowned(Guid agentId, UsableMissionObject point) =>
        appliedSeats.TryGetValue(agentId, out var seat) && seat.LeftLocally && ReferenceEquals(seat.Point, point);

    // The owner's release shows the puppet's local release was its own, so the refresh's unowned count is withdrawn and its
    // re-seat undone; nothing else is left to release.
    private bool SettleLocalRelease(NetworkAgentStationUse use, Agent agent, UsableMissionObject point)
    {
        if (CountedUnowned(use.AgentId, point)) unownedPuppetReleases--;
        appliedSeats.Remove(use.AgentId);
        if (engine.IsSeated(agent, point)) Apply(agent, point, inUse: false);
        return SettleObsoleteRelease(use, "its puppet left the station here first");
    }

    // Keeps the seat an applied use put the puppet in, or forgets it when the use released the puppet.
    internal void RecordAppliedSeat(Guid agentId, Agent agent, MissionObject hull, UsableMissionObject point)
    {
        if (point == null) appliedSeats.Remove(agentId);
        else appliedSeats[agentId] = new AppliedSeat(agent, hull, point);
    }

    // The station owns the puppet's pose from here, so the buffered owner pose is dropped before seating.
    internal void Apply(Agent agent, UsableMissionObject point, bool inUse)
    {
        missionComponent.AgentMovementHandler.ForgetMovementTarget(agent);
        engine.ApplyStationUse(agent, point, inUse);
        applied++;
    }

    // Notes a joiner, whose seat catch-up the next tick sends.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
            if (disposed || session.IsOwn(entered.ControllerId)) return;

            joinersAwaitingSeats.Add(entered.ControllerId);
        }, context: nameof(Handle_PeerEntered));
    }

    // The ship replicator sends a joiner's hull and crew records from the arrivals at the start of this tick, so the seats
    // follow them; a record that lands a frame later is absorbed by the joiner's pending retry. Before the deployment commit
    // the commit's announcement covers the joiner.
    private void SendSeatsToJoiners()
    {
        if (joinersAwaitingSeats.Count == 0) return;

        if (seatsAnnounced)
        {
            foreach (var joiner in joinersAwaitingSeats)
                foreach (var use in ownSeated.Values)
                    network.Send(joiner, use);
        }

        joinersAwaitingSeats.Clear();
    }

    public string DescribeStation(Agent agent) => DescribeStation(agent?.CurrentlyUsedGameObject);

    // The point as hull id, station path and index, or null when it is no station of a registered hull.
    private string DescribeStation(UsableMissionObject point)
    {
        if (point == null || !engine.TryDescribeStation(point, out var hull, out var stationKey, out int pointIndex)
            || !missionComponent.ShipRegistry.TryGetByHull(hull, out var ship))
            return null;

        return ship.ShipId + "/" + stationKey + "#" + pointIndex;
    }

    // The counters and collection sizes for the naval inspect command.
    public object Inspect() => new
    {
        sent,
        applied,
        dropped,
        staleUses,
        obsoleteReleases,
        unownedPuppetReleases,
        pinned,
        ownerReleases,
        sinkingUseReleases,
        sinkingSafetyNetReleases,
        withdrawnReleases,
        removedHullSeats,
        departedHullReleases,
        detachedSeats,
        adoptedSeats,
        appliedSeats = appliedSeats.Count,
        pending = pending.Count,
        ownSeated = ownSeated.Count,
    };

    // A remote owner's seat applied to its puppet here: the puppet, the hull and the point it sits at.
    private sealed class AppliedSeat
    {
        public AppliedSeat(Agent agent, MissionObject hull, UsableMissionObject point)
        {
            Agent = agent;
            Hull = hull;
            Point = point;
        }

        public Agent Agent { get; }
        public MissionObject Hull { get; }
        public UsableMissionObject Point { get; }
        // Set once vanilla released the puppet here and the refresh re-seated it; the owner's next revision settles the release.
        public bool LeftLocally { get; set; }
    }

    // A received use waiting for the tick, with the time it arrived.
    private readonly struct PendingUse
    {
        public PendingUse(NetworkAgentStationUse use, float receivedAt)
        {
            Use = use;
            ReceivedAt = receivedAt;
        }

        public NetworkAgentStationUse Use { get; }
        public float ReceivedAt { get; }
    }
}
