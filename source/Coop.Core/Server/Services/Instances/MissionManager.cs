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

    /// <summary>Resolve a relay target only when source and target are current members of the named instance.</summary>
    bool TryGetRelayTarget(NetPeer sourcePeer, string instanceId, string controllerId, out NetPeer peer);

    /// <summary>Immediately fence relay traffic tied to a peer before its queued leave or disconnect.</summary>
    void RevokeRelay(NetPeer peer);

    /// <summary>
    /// Record that <paramref name="controllerId"/> has entered <paramref name="instanceId"/>, mapping it to
    /// the connection the announcement arrived on (<paramref name="peer"/>) so the relay fallback can reach
    /// it. Creates the instance if this is its first member. Driven by a client <c>MissionEntered</c>.
    /// Returns false after result finalization claimed the instance. The result reports every membership
    /// change made atomically before the new entry.
    /// </summary>
    bool TryEnterMission(
        NetPeer peer,
        string controllerId,
        string instanceId,
        out MissionEntryResult result);

    /// <summary>
    /// Record that <paramref name="controllerId"/> (on <paramref name="peer"/>) has left
    /// <paramref name="instanceId"/>, dropping it from the relay routing table. Returns false unless that
    /// exact authoritative membership existed; otherwise returns the membership actually removed.
    /// </summary>
    bool TryLeaveMission(NetPeer peer, string controllerId, string instanceId, out MissionDeparture departure);

    /// <summary>
    /// Drop every membership still tied to <paramref name="peer"/> after an ungraceful disconnect.
    /// </summary>
    IReadOnlyList<MissionDeparture> HandleDisconnect(NetPeer peer);

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

public enum MissionEntryStatus
{
    Entered,
    Reconnected,
    Unchanged,
}

public sealed class MissionEntryResult
{
    public string ControllerId { get; }
    public string InstanceId { get; }
    public MissionEntryStatus Status { get; }
    public IReadOnlyList<(string controllerId, NetPeer peer)> ExistingMembers { get; }
    public IReadOnlyList<MissionDeparture> PreviousDepartures { get; }
    public bool IsFirstMember { get; }

    public MissionEntryResult(
        string controllerId,
        string instanceId,
        MissionEntryStatus status,
        IReadOnlyList<(string controllerId, NetPeer peer)> existingMembers,
        IReadOnlyList<MissionDeparture> previousDepartures,
        bool isFirstMember)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        Status = status;
        ExistingMembers = existingMembers;
        PreviousDepartures = previousDepartures;
        IsFirstMember = isFirstMember;
    }
}

public sealed class MissionDeparture
{
    public string ControllerId { get; }
    public string InstanceId { get; }
    public IReadOnlyList<(string controllerId, NetPeer peer)> RemainingMembers { get; }
    public bool IsInstanceEmpty => RemainingMembers.Count == 0;

    public MissionDeparture(
        string controllerId,
        string instanceId,
        IReadOnlyList<(string controllerId, NetPeer peer)> remainingMembers)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        RemainingMembers = remainingMembers;
    }
}

/// <inheritdoc cref="IMissionManager"/>
public class MissionManager : IMissionManager, IMissionMembershipRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionManager>();

    private readonly object gate = new object();
    private readonly Dictionary<string, MissionInstance> byInstanceId = new Dictionary<string, MissionInstance>();
    private readonly Dictionary<NetPeer, MissionMembership> byPeer = new Dictionary<NetPeer, MissionMembership>();
    private readonly Dictionary<string, MissionMembership> byController = new Dictionary<string, MissionMembership>();
    private readonly Dictionary<NetPeer, long> relayRevocationCounts = new Dictionary<NetPeer, long>();
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

    public bool TryGetRelayTarget(NetPeer sourcePeer, string instanceId, string controllerId, out NetPeer peer)
    {
        peer = null;

        if (sourcePeer == null || string.IsNullOrEmpty(instanceId))
            return false;

        if (string.IsNullOrEmpty(controllerId))
            return false;

        lock (gate)
        {
            if (!byPeer.TryGetValue(sourcePeer, out var sourceMembership) ||
                sourceMembership.Instance.Id != instanceId ||
                IsRelayRevoked(sourceMembership))
            {
                return false;
            }

            if (!byController.TryGetValue(controllerId, out var targetMembership) ||
                !ReferenceEquals(sourceMembership.Instance, targetMembership.Instance) ||
                IsRelayRevoked(targetMembership))
            {
                return false;
            }

            peer = targetMembership.Peer;
            return true;
        }
    }

    public void RevokeRelay(NetPeer peer)
    {
        if (peer == null)
            return;

        lock (gate)
        {
            relayRevocationCounts.TryGetValue(peer, out var count);
            relayRevocationCounts[peer] = count + 1;
        }
    }

    public bool TryEnterMission(
        NetPeer peer,
        string controllerId,
        string instanceId,
        out MissionEntryResult result)
    {
        result = null;
        if (peer == null || string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(instanceId))
            return false;

        lock (gate)
        {
            if (IsConclusionFenced(instanceId))
            {
                Logger.Information("Ignoring mission entry by {Controller} for concluded instance {Instance}",
                    controllerId, instanceId);
                return false;
            }

            byPeer.TryGetValue(peer, out var peerMembership);
            byController.TryGetValue(controllerId, out var controllerMembership);

            if (ReferenceEquals(peerMembership, controllerMembership) &&
                peerMembership != null &&
                peerMembership.Instance.Id == instanceId)
            {
                result = new MissionEntryResult(
                    controllerId,
                    instanceId,
                    MissionEntryStatus.Unchanged,
                    Array.Empty<(string, NetPeer)>(),
                    Array.Empty<MissionDeparture>(),
                    isFirstMember: false);
                return true;
            }

            var previousDepartures = new List<MissionDeparture>();
            if (controllerMembership != null && controllerMembership.Instance.Id == instanceId)
            {
                if (peerMembership != null && !ReferenceEquals(peerMembership, controllerMembership))
                    previousDepartures.Add(RemoveMembership(peerMembership));

                byPeer.Remove(controllerMembership.Peer);
                controllerMembership.Peer = peer;
                byPeer[peer] = controllerMembership;

                var existingMembers = Members(controllerMembership.Instance, controllerId);
                result = new MissionEntryResult(
                    controllerId,
                    instanceId,
                    MissionEntryStatus.Reconnected,
                    existingMembers,
                    previousDepartures,
                    isFirstMember: false);
                Logger.Information("Controller {Controller} replaced its peer in instance {Instance} with {Peer}",
                    controllerId, instanceId, peer);
                return true;
            }

            if (peerMembership != null)
                previousDepartures.Add(RemoveMembership(peerMembership));
            if (controllerMembership != null && !ReferenceEquals(controllerMembership, peerMembership))
                previousDepartures.Add(RemoveMembership(controllerMembership));

            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                instance = new MissionInstance(instanceId);
                byInstanceId[instanceId] = instance;
                Logger.Information("Created instance {Instance} on first mission entry by {Controller}",
                    instanceId, controllerId);
            }

            bool isFirstMember = instance.Memberships.Count == 0;
            var others = Members(instance);
            var membership = new MissionMembership(controllerId, peer, instance);
            instance.Memberships.Add(membership);
            byPeer[peer] = membership;
            byController[controllerId] = membership;

            Logger.Information("Controller {Controller} entered instance {Instance} on {Peer}",
                controllerId, instanceId, peer);

            result = new MissionEntryResult(
                controllerId,
                instanceId,
                MissionEntryStatus.Entered,
                others,
                previousDepartures,
                isFirstMember);
            return true;
        }
    }

    public bool TryLeaveMission(NetPeer peer, string controllerId, string instanceId, out MissionDeparture departure)
    {
        departure = null;
        if (peer == null)
            return false;

        lock (gate)
        {
            CompleteRelayRevocation(peer);

            if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(instanceId))
                return false;

            if (!byPeer.TryGetValue(peer, out var membership) ||
                membership.ControllerId != controllerId ||
                membership.Instance.Id != instanceId)
            {
                Logger.Warning("Ignoring unmatched mission leave for instance {Instance} from {Controller}",
                    instanceId, controllerId);
                return false;
            }

            departure = RemoveMembership(membership);
            Logger.Information("Controller {Controller} left instance {Instance}", controllerId, instanceId);
            return true;
        }
    }

    public IReadOnlyList<MissionDeparture> HandleDisconnect(NetPeer peer)
    {
        if (peer == null)
            return Array.Empty<MissionDeparture>();

        lock (gate)
        {
            var staleMemberships = byInstanceId.Values
                .SelectMany(instance => instance.Memberships)
                .Where(membership => ReferenceEquals(membership.Peer, peer))
                .Distinct()
                .ToList();

            var departures = new List<MissionDeparture>(staleMemberships.Count);
            foreach (var membership in staleMemberships)
            {
                var departure = RemoveMembership(membership);
                departures.Add(departure);
                Logger.Information("Controller {Controller} disconnected from instance {Instance}",
                    departure.ControllerId, departure.InstanceId);
            }

            CompleteRelayRevocation(peer);

            return departures;
        }
    }

    public bool IsControllerInMission(string controllerId)
    {
        if (controllerId == null)
            return false;

        lock (gate)
        {
            return byController.ContainsKey(controllerId);
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

    private bool IsRelayRevoked(MissionMembership membership) =>
        relayRevocationCounts.ContainsKey(membership.Peer);

    private void CompleteRelayRevocation(NetPeer peer)
    {
        if (!relayRevocationCounts.TryGetValue(peer, out var count))
            return;

        if (count == 1)
            relayRevocationCounts.Remove(peer);
        else
            relayRevocationCounts[peer] = count - 1;
    }

    // Snapshot the (controllerId, peer) pairs still routed through the instance. Caller holds the lock.
    private MissionDeparture RemoveMembership(MissionMembership membership)
    {
        membership.Instance.Memberships.Remove(membership);
        if (byPeer.TryGetValue(membership.Peer, out var peerMembership) &&
            ReferenceEquals(peerMembership, membership))
        {
            byPeer.Remove(membership.Peer);
        }
        if (byController.TryGetValue(membership.ControllerId, out var controllerMembership) &&
            ReferenceEquals(controllerMembership, membership))
        {
            byController.Remove(membership.ControllerId);
        }

        var remaining = Members(membership.Instance);
        PruneIfEmpty(membership.Instance.Id, remaining.Count);
        return new MissionDeparture(membership.ControllerId, membership.Instance.Id, remaining);
    }

    private static IReadOnlyList<(string controllerId, NetPeer peer)> Members(
        MissionInstance instance,
        string excludedControllerId = null)
        => instance.Memberships
            .Where(member => member.ControllerId != excludedControllerId)
            .Select(member => (member.ControllerId, member.Peer))
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
