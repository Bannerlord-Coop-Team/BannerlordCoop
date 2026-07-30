using Common.Logging;
using Common.Network.Data;
using GameInterface.Services.Missions;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Co-hosted NAT-punch rendezvous for P2P mission instances (taverns etc.). Instance ids are derived
/// client-side from (settlement, location), so co-located clients independently arrive at the same id.
/// The server simply introduces every peer that punches into a given instance to the others, creating
/// the instance on the first punch — there is no server-issued assignment.
/// </summary>
public interface IMissionManager
{
    /// <summary>
    /// Handle a NAT-introduction request: introduce the requesting peer to every other peer already
    /// punched into the same instance. Driven purely by the request's <see cref="ConnectionToken"/>,
    /// whose instance name is the client-derived instance id.
    /// </summary>
    void HandleIntroductionRequest(NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);

    bool TryGetRelayTarget(string instanceId, string controllerId, out NetPeer peer);

    /// <summary>
    /// Record that <paramref name="controllerId"/> has entered <paramref name="instanceId"/>, mapping it to
    /// the connection the announcement arrived on (<paramref name="peer"/>) so the relay fallback can reach
    /// it. Creates the instance if this is its first member. Driven by a client <c>MissionEntered</c>.
    /// Returns false after result finalization claimed the empty instance. On success, returns whether this is
    /// the first member and the members already present so the caller can publish post-entry state and introductions.
    /// </summary>
    bool TryEnterMission(
        NetPeer peer,
        string controllerId,
        string instanceId,
        out IReadOnlyList<(string controllerId, NetPeer peer)> existingMembers,
        out bool isFirstMember);

    /// <summary>
    /// Record that <paramref name="controllerId"/> (on <paramref name="peer"/>) has left
    /// <paramref name="instanceId"/>, dropping it from the relay routing table. No-op if the instance is
    /// unknown. Driven by a client <c>MissionLeft</c>. Returns the members still present so the caller can
    /// notify them the controller is gone.
    /// </summary>
    IReadOnlyList<(string controllerId, NetPeer peer)> LeaveMission(NetPeer peer, string controllerId, string instanceId);

    /// <summary>
    /// Drop <paramref name="peer"/> from whichever instance it belongs to after an ungraceful disconnect,
    /// resolving its <paramref name="controllerId"/> and <paramref name="instanceId"/>. Returns false if the
    /// peer was in no instance. On success <paramref name="remaining"/> holds the members still present so
    /// the caller can notify them.
    /// </summary>
    bool TryHandleDisconnect(NetPeer peer, out string controllerId, out string instanceId,
        out IReadOnlyList<(string controllerId, NetPeer peer)> remaining);

    /// <summary>
    /// The controllers currently routed through <paramref name="instanceId"/> (relay-fallback membership).
    /// Returns false if the instance is unknown.
    /// </summary>
    bool TryGetControllers(string instanceId, out IReadOnlyCollection<string> controllers);

    /// <summary>
    /// Atomically fences entry when the current controllers still match the result decision's snapshot.
    /// </summary>
    bool TryBeginActiveInstanceConclusion(
        string instanceId,
        IReadOnlyCollection<string> expectedControllers);

    /// <summary>
    /// Atomically begins finalizing an empty instance while entry is fenced.
    /// </summary>
    bool TryBeginEmptyInstanceConclusion(string instanceId);

    /// <summary>Commits a successful conclusion or rolls its entry fence back after a failed apply.</summary>
    bool CompleteInstanceConclusion(string instanceId, bool succeeded);
}

/// <inheritdoc cref="IMissionManager"/>
public class MissionManager : IMissionManager, IMissionMembershipRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionManager>();

    private readonly object gate = new object();
    private readonly Dictionary<string, MissionInstance> byInstanceId = new Dictionary<string, MissionInstance>();
    private readonly Dictionary<string, MissionInstance> pendingEmptyInstances = new Dictionary<string, MissionInstance>();
    private readonly HashSet<string> concludingInstances = new HashSet<string>();
    private readonly HashSet<string> concludedInstances = new HashSet<string>();

    public void HandleIntroductionRequest(
        NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            Logger.Warning("Discarding NAT introduction with unparseable token from {Endpoint}", remoteEndPoint);
            return;
        }

        string instanceId = connectionToken.InstanceId;

        lock (gate)
        {
            if (IsConclusionFenced(instanceId))
            {
                Logger.Information("Ignoring NAT introduction for concluded instance {Instance}", instanceId);
                return;
            }

            // Instance ids are derived client-side from (settlement, location), so co-located clients
            // independently arrive at the same id. The first punch for an id creates the instance; the
            // rest are introduced into it. No separate server-assignment round-trip is needed.
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                instance = new MissionInstance(instanceId);
                byInstanceId[instanceId] = instance;
                Logger.Information("Created instance {Instance} on first NAT punch from {Endpoint}",
                    instanceId, remoteEndPoint);
            }

            // A punch = (re)entering now. Drop any earlier slot for this endpoint first, else a re-joiner
            // (same endpoint, since the socket persists) is mistaken for a duplicate and never reconnected.
            RemoveEndpointEverywhere(remoteEndPoint);

            foreach (var existing in instance.PunchEndpoints)
            {
                Logger.Information("Introducing {Newcomer} <-> {Existing} for instance {Instance}",
                    remoteEndPoint, existing.External, instanceId);

                natPunchModule.NatIntroduce(
                    existing.Internal, existing.External, // host side
                    localEndPoint, remoteEndPoint,        // newcomer side
                    token);
            }

            instance.PunchEndpoints.Add(new MissionInstance.Endpoints(localEndPoint, remoteEndPoint));
        }
    }

    public bool TryGetRelayTarget(string instanceId, string controllerId, out NetPeer peer)
    {
        peer = null;

        if (instanceId == null)
            return false;

        if (controllerId == null)
            return false;

        lock (gate)
        {
            if (!byInstanceId.TryGetValue(instanceId, out var instance))
                return false;

            return instance.TryGetPeer(controllerId, out peer);
        }
    }

    public bool TryEnterMission(
        NetPeer peer,
        string controllerId,
        string instanceId,
        out IReadOnlyList<(string controllerId, NetPeer peer)> existingMembers,
        out bool isFirstMember)
    {
        lock (gate)
        {
            if (IsConclusionFenced(instanceId))
            {
                existingMembers = Array.Empty<(string, NetPeer)>();
                isFirstMember = false;
                Logger.Information("Ignoring mission entry by {Controller} for concluded instance {Instance}",
                    controllerId, instanceId);
                return false;
            }

            // Shares the instance dictionary with the NAT-punch flow, so the relay context and the punch
            // endpoints for one mission live in the SAME MissionInstance — provided both sides derive the
            // same instance id (see MissionEntered).
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                instance = new MissionInstance(instanceId);
                byInstanceId[instanceId] = instance;
                Logger.Information("Created instance {Instance} on first mission entry by {Controller}",
                    instanceId, controllerId);
            }

            // Snapshot the members already present BEFORE adding the newcomer, so the caller can introduce
            // the newcomer and the existing members to each other.
            isFirstMember = instance.Controllers.Count == 0;
            existingMembers = instance.Controllers
                .Where(id => id != controllerId)
                .Select(id => instance.TryGetPeer(id, out var existingPeer) ? (id, existingPeer) : default)
                .Where(pair => pair.Item2 != null)
                .ToList();

            instance.MapPeer(controllerId, peer);
            Logger.Information("Controller {Controller} entered instance {Instance} on {Peer}",
                controllerId, instanceId, peer);

            return true;
        }
    }

    public IReadOnlyList<(string controllerId, NetPeer peer)> LeaveMission(NetPeer peer, string controllerId, string instanceId)
    {
        lock (gate)
        {
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                Logger.Warning("Mission leave for unknown instance {Instance} from {Controller}",
                    instanceId, controllerId);
                return Array.Empty<(string, NetPeer)>();
            }

            instance.RemovePeer(peer);
            Logger.Information("Controller {Controller} left instance {Instance}", controllerId, instanceId);

            var remaining = Members(instance);
            PruneIfEmpty(instanceId, remaining.Count);
            return remaining;
        }
    }

    public bool TryHandleDisconnect(NetPeer peer, out string controllerId, out string instanceId,
        out IReadOnlyList<(string controllerId, NetPeer peer)> remaining)
    {
        controllerId = null;
        instanceId = null;
        remaining = Array.Empty<(string, NetPeer)>();

        lock (gate)
        {
            // A peer is in at most one instance; find the one that still lists this connection.
            MissionInstance found = null;
            foreach (var entry in byInstanceId)
            {
                if (entry.Value.TryGetController(peer, out controllerId) == false) continue;

                instanceId = entry.Key;
                found = entry.Value;
                break; // leave the enumeration before mutating the dictionary (PruneIfEmpty removes the entry)
            }

            if (found == null)
                return false;

            found.RemovePeer(peer);
            remaining = Members(found);
            Logger.Information("Controller {Controller} disconnected from instance {Instance}", controllerId, instanceId);
            PruneIfEmpty(instanceId, remaining.Count);
            return true;
        }
    }

    public bool IsControllerInMission(string controllerId)
    {
        if (controllerId == null)
            return false;

        lock (gate)
        {
            return byInstanceId.Values.Any(instance => instance.TryGetPeer(controllerId, out _));
        }
    }

    public bool IsInstanceOccupied(string instanceId)
    {
        if (instanceId == null)
            return false;

        lock (gate)
        {
            return byInstanceId.TryGetValue(instanceId, out var instance) && instance.Controllers.Count > 0;
        }
    }

    // Drop the instance record once its last member is gone (BR-017: destroying the battle instance includes
    // the membership/relay record — previously it leaked per battle). Any stale NAT-punch endpoints go with
    // it; a later (re-)engagement of the same instance id re-punches and recreates the record from scratch,
    // which is exactly the fresh instance BR-054/BR-002 call for. Caller holds the lock.
    private void PruneIfEmpty(string instanceId, int remainingMembers)
    {
        if (remainingMembers > 0)
            return;

        byInstanceId.Remove(instanceId);
        Logger.Information("Removed empty instance {Instance} after its last member left", instanceId);
    }

    public bool TryGetControllers(string instanceId, out IReadOnlyCollection<string> controllers)
    {
        lock (gate)
        {
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false ||
                instance.Controllers.Count == 0)
            {
                controllers = Array.Empty<string>();
                return false;
            }

            // MissionInstance.Controllers already returns a snapshot array — safe to hand out.
            controllers = instance.Controllers;
            return true;
        }
    }

    public bool TryBeginEmptyInstanceConclusion(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return false;

        lock (gate)
        {
            if ((byInstanceId.TryGetValue(instanceId, out var instance) && instance.Controllers.Count > 0) ||
                IsConclusionFenced(instanceId))
            {
                return false;
            }

            concludingInstances.Add(instanceId);
            if (instance != null)
                pendingEmptyInstances[instanceId] = instance;
            byInstanceId.Remove(instanceId);
            return true;
        }
    }

    public bool TryBeginActiveInstanceConclusion(
        string instanceId,
        IReadOnlyCollection<string> expectedControllers)
    {
        if (string.IsNullOrEmpty(instanceId) || expectedControllers == null || expectedControllers.Count == 0)
            return false;

        lock (gate)
        {
            if (!byInstanceId.TryGetValue(instanceId, out var instance) ||
                IsConclusionFenced(instanceId))
            {
                return false;
            }

            var currentControllers = instance.Controllers;
            if (currentControllers.Count != expectedControllers.Count ||
                currentControllers.Any(controllerId => !expectedControllers.Contains(controllerId)))
            {
                return false;
            }

            return concludingInstances.Add(instanceId);
        }
    }

    public bool CompleteInstanceConclusion(string instanceId, bool succeeded)
    {
        if (string.IsNullOrEmpty(instanceId))
            return false;

        lock (gate)
        {
            if (!concludingInstances.Remove(instanceId))
                return false;

            if (succeeded)
            {
                concludedInstances.Add(instanceId);
                pendingEmptyInstances.Remove(instanceId);
                return true;
            }

            if (pendingEmptyInstances.TryGetValue(instanceId, out var emptyInstance))
            {
                byInstanceId[instanceId] = emptyInstance;
                pendingEmptyInstances.Remove(instanceId);
            }

            return true;
        }
    }

    // Caller holds gate when this is used from a mutation path.
    private bool IsConclusionFenced(string instanceId) =>
        concludingInstances.Contains(instanceId) || concludedInstances.Contains(instanceId);

    // Snapshot the (controllerId, peer) pairs still routed through the instance. Caller holds the lock.
    private static IReadOnlyList<(string controllerId, NetPeer peer)> Members(MissionInstance instance)
        => instance.Controllers
            .Select(id => instance.TryGetPeer(id, out var peer) ? (id, peer) : default)
            .Where(pair => pair.Item2 != null)
            .ToList();

    // A peer is in at most one instance, so any prior listing for this endpoint is stale on a new punch.
    private void RemoveEndpointEverywhere(IPEndPoint external)
    {
        foreach (var instance in byInstanceId.Values)
        {
            instance.PunchEndpoints.RemoveAll(e => e.External.Equals(external));
        }
    }
}
