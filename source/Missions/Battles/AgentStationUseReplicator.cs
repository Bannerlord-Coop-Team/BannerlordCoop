using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates ship station use: the owner of an agent sends every use and release of a hull's usable point, and peers
/// seat or release their puppet on the same point of their hull. Agents seated by their owner send no world movement.
/// </summary>
public interface IAgentStationUseReplicator : IDisposable
{
    /// <summary>[Game thread] Retry station uses whose agent or hull has not arrived yet.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] The station an agent uses on a registered hull, or null (diagnostics).</summary>
    string DescribeStation(Agent agent);

    /// <summary>Whether this client seated the agent and withholds its movement and actions (diagnostics).</summary>
    bool IsOwnSeat(System.Guid agentId);

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

    private readonly Dictionary<Guid, long> sentRevisions = new Dictionary<Guid, long>();
    private readonly Dictionary<Guid, NetworkAgentStationUse> ownSeated = new Dictionary<Guid, NetworkAgentStationUse>();
    private readonly Dictionary<Guid, long> appliedRevisions = new Dictionary<Guid, long>();
    private readonly List<PendingUse> pending = new List<PendingUse>();
    private readonly Dictionary<Guid, AppliedSeat> appliedSeats = new Dictionary<Guid, AppliedSeat>();
    private float elapsed;
    private bool seatsAnnounced;
    private bool applyingRemote;
    private long sent, applied, dropped, localPuppetReleases, pinned;

    public AgentStationUseReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        ICoopMissionComponent missionComponent,
        INavalShipEngine engine,
        IBattleDeploymentCoordinator deployment)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.deployment = deployment;

        missionComponent.AgentMovementHandler.ConfigureSeatedMovement(IsSeatedByOwner);
        missionComponent.AgentActionHandler.ConfigureStationOwnedAgents(IsSeatedByOwner);
        messageBroker.Subscribe<AgentStationUseChanged>(Handle_AgentStationUseChanged);
        messageBroker.Subscribe<NetworkAgentStationUse>(Handle_NetworkAgentStationUse);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    public void Dispose()
    {
        missionComponent.AgentMovementHandler.ConfigureSeatedMovement(null);
        missionComponent.AgentActionHandler.ConfigureStationOwnedAgents(null);
        messageBroker.Unsubscribe<AgentStationUseChanged>(Handle_AgentStationUseChanged);
        messageBroker.Unsubscribe<NetworkAgentStationUse>(Handle_NetworkAgentStationUse);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    // [Game thread] Published by the use/release patches; only agents this client drives are announced.
    private void Handle_AgentStationUseChanged(MessagePayload<AgentStationUseChanged> payload)
    {
        var change = payload.What;
        var agents = missionComponent.AgentRegistry;
        if (change.Agent == null || !agents.TryGetAgentInfo(change.Agent, out var info)) return;

        if (!agents.IsLocallyControlled(change.Agent))
        {
            // Only the owner's stream may move a puppet off a station; anything else is a local desync worth seeing.
            if (!applyingRemote && !change.InUse)
            {
                Logger.Warning("[NavalSync] Puppet {AgentId} left a station locally, not by its owner", info.AgentId);
            }
            return;
        }

        if (!engine.TryDescribeStation(change.Point, out var hull, out var stationKey, out int pointIndex)
            || !missionComponent.ShipRegistry.TryGetByHull(hull, out var ship))
            return;

        sentRevisions.TryGetValue(info.AgentId, out long revision);
        var use = new NetworkAgentStationUse(info.AgentId, ship.ShipId, stationKey, pointIndex, change.InUse, revision + 1);
        sentRevisions[info.AgentId] = use.Revision;
        if (use.InUse) ownSeated[info.AgentId] = use;
        else ownSeated.Remove(info.AgentId);

        // Before the deployment commit peers have no own crew yet; the commit announces the seats taken by then.
        if (!seatsAnnounced) return;

        network.SendAll(use);
        sent++;
    }

    private bool IsSeatedByOwner(CoopAgentInfo info) => info != null && ownSeated.ContainsKey(info.AgentId);

    public bool IsOwnSeat(Guid agentId) => ownSeated.ContainsKey(agentId);

    private void Handle_NetworkAgentStationUse(MessagePayload<NetworkAgentStationUse> payload)
    {
        var use = payload.What;
        GameThread.RunSafe(() =>
        {
            if (use == null) return;
            pending.Add(new PendingUse(use, elapsed));
            DrainPending();
        }, context: nameof(Handle_NetworkAgentStationUse));
    }

    public void Tick(float dt)
    {
        elapsed += Math.Max(0f, dt);
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

    // [Game thread] Runs after this frame's hull frame writes. A puppet the owner keeps seated is re-pinned to its station's
    // user frame on the moved hull, as the lab did; if vanilla released it locally it is re-seated once per owner revision.
    internal void RefreshAppliedSeats()
    {
        if (appliedSeats.Count == 0) return;

        var leftAgents = new List<Guid>();
        foreach (var entry in appliedSeats)
        {
            var seat = entry.Value;
            if (!engine.IsAlive(seat.Agent))
            {
                leftAgents.Add(entry.Key);
                continue;
            }

            if (!engine.IsSeated(seat.Agent, seat.Point))
            {
                if (seat.Reseated) continue;

                seat.Reseated = true;
                localPuppetReleases++;
                Logger.Warning("[NavalSync] Puppet {AgentId} was released from its station locally; re-seating it once", entry.Key);
                Apply(seat.Agent, seat.Point, inUse: true);
                if (!engine.IsSeated(seat.Agent, seat.Point)) continue;
            }

            engine.PinToStation(seat.Agent, seat.Point);
            pinned++;
        }

        foreach (var agentId in leftAgents)
            appliedSeats.Remove(agentId);
    }

    private void DrainPending()
    {
        if (pending.Count == 0) return;
        pending.RemoveAll(entry => TryApply(entry.Use) || Expire(entry));
    }

    private bool Expire(PendingUse entry)
    {
        if (elapsed - entry.ReceivedAt < PendingLifetimeSeconds) return false;

        dropped++;
        Logger.Warning("[NavalSync] Dropped station use {Revision} of agent {AgentId}: agent or hull {ShipId} never arrived",
            entry.Use.Revision, entry.Use.AgentId, entry.Use.ShipId);
        return true;
    }

    // [Game thread] True when settled (applied or dropped), false to retry while the agent or hull is still missing.
    private bool TryApply(NetworkAgentStationUse use)
    {
        var agents = missionComponent.AgentRegistry;
        if (!agents.TryGetAgentInfo(use.AgentId, out var info) || info.Agent == null
            || !missionComponent.ShipRegistry.TryGet(use.ShipId, out var ship))
            return false;

        appliedRevisions.TryGetValue(use.AgentId, out long appliedRevision);
        if (!IsNewer(appliedRevision, use.Revision) || agents.IsLocallyControlled(info.Agent))
        {
            dropped++;
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

        appliedRevisions[use.AgentId] = use.Revision;
        Apply(info.Agent, point, use.InUse);
        RecordAppliedSeat(use.AgentId, info.Agent, use.InUse ? point : null);
        return true;
    }

    internal void RecordAppliedSeat(Guid agentId, Agent agent, UsableMissionObject point)
    {
        if (point == null) appliedSeats.Remove(agentId);
        else appliedSeats[agentId] = new AppliedSeat(agent, point);
    }

    // The station owns the puppet's pose from here, so the buffered owner pose is dropped before seating.
    internal void Apply(Agent agent, UsableMissionObject point, bool inUse)
    {
        missionComponent.AgentMovementHandler.ForgetMovementTarget(agent);
        applyingRemote = true;
        try
        {
            engine.ApplyStationUse(agent, point, inUse);
        }
        finally
        {
            applyingRemote = false;
        }

        applied++;
    }

    /// <summary>Whether a received revision supersedes the last one applied for the agent.</summary>
    internal static bool IsNewer(long appliedRevision, long receivedRevision) => receivedRevision > appliedRevision;

    // A joiner gets this client's current seats after the hull and crew records that precede them.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
            if (!seatsAnnounced || session.IsOwn(entered.ControllerId)) return;

            foreach (var use in ownSeated.Values)
                network.Send(entered.ControllerId, use);
        }, context: nameof(Handle_PeerEntered));
    }

    public string DescribeStation(Agent agent)
    {
        var point = agent?.CurrentlyUsedGameObject;
        if (point == null || !engine.TryDescribeStation(point, out var hull, out var stationKey, out int pointIndex)
            || !missionComponent.ShipRegistry.TryGetByHull(hull, out var ship))
            return null;

        return ship.ShipId + "/" + stationKey + "#" + pointIndex;
    }

    public object Inspect() => new
    {
        sent,
        applied,
        dropped,
        localPuppetReleases,
        pinned,
        appliedSeats = appliedSeats.Count,
        pending = pending.Count,
        ownSeated = ownSeated.Count,
    };

    private sealed class AppliedSeat
    {
        public AppliedSeat(Agent agent, UsableMissionObject point)
        {
            Agent = agent;
            Point = point;
        }

        public Agent Agent { get; }
        public UsableMissionObject Point { get; }
        // Set once a local release was re-seated; the next owner revision gives the seat a fresh attempt.
        public bool Reseated { get; set; }
    }

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
