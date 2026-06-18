using Common.Logging;
using Common.Network.Data;
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

    /// <summary>
    /// Resolve the live server connections for <paramref name="controllerIds"/> within
    /// <paramref name="instanceId"/>, so a <c>RelayPacket</c> can be forwarded to them (the NAT-punch relay
    /// fallback). Returns an empty list if the instance is unknown. Snapshotted under the instance lock so
    /// the caller can send without holding it.
    /// </summary>
    IReadOnlyList<NetPeer> GetRelayTargets(string instanceId, IEnumerable<string> controllerIds);

    /// <summary>
    /// Record that <paramref name="controllerId"/> has entered <paramref name="instanceId"/>, mapping it to
    /// the connection the announcement arrived on (<paramref name="peer"/>) so the relay fallback can reach
    /// it. Creates the instance if this is its first member. Driven by a client <c>MissionEntered</c>.
    /// Returns the members already present (excluding the newcomer) so the caller can introduce them to it.
    /// </summary>
    IReadOnlyList<(string controllerId, NetPeer peer)> EnterMission(NetPeer peer, string controllerId, string instanceId);

    /// <summary>
    /// Record that <paramref name="controllerId"/> (on <paramref name="peer"/>) has left
    /// <paramref name="instanceId"/>, dropping it from the relay routing table. No-op if the instance is
    /// unknown. Driven by a client <c>MissionLeft</c>.
    /// </summary>
    void LeaveMission(NetPeer peer, string controllerId, string instanceId);
}

/// <inheritdoc cref="IMissionManager"/>
public class MissionManager : IMissionManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionManager>();

    private readonly object gate = new object();
    private readonly Dictionary<string, MissionInstance> byInstanceId = new Dictionary<string, MissionInstance>();

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

    public IReadOnlyList<NetPeer> GetRelayTargets(string instanceId, IEnumerable<string> controllerIds)
    {
        lock (gate)
        {
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                Logger.Warning("Discarding relay for unknown instance {Instance}", instanceId);
                return Array.Empty<NetPeer>();
            }

            // Materialize inside the lock: GetPeers is a lazy iterator over the instance's routing table,
            // so snapshot it now and let the handler send after releasing the lock.
            return instance.GetPeers(controllerIds).ToList();
        }
    }

    public IReadOnlyList<(string controllerId, NetPeer peer)> EnterMission(NetPeer peer, string controllerId, string instanceId)
    {
        lock (gate)
        {
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
            var others = instance.Controllers
                .Where(id => id != controllerId)
                .Select(id => instance.TryGetPeer(id, out var existingPeer) ? (id, existingPeer) : default)
                .Where(pair => pair.Item2 != null)
                .ToList();

            instance.MapPeer(controllerId, peer);
            Logger.Information("Controller {Controller} entered instance {Instance} on {Peer}",
                controllerId, instanceId, peer);

            return others;
        }
    }

    public void LeaveMission(NetPeer peer, string controllerId, string instanceId)
    {
        lock (gate)
        {
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                Logger.Warning("Mission leave for unknown instance {Instance} from {Controller}",
                    instanceId, controllerId);
                return;
            }

            instance.RemovePeer(peer);
            Logger.Information("Controller {Controller} left instance {Instance}", controllerId, instanceId);
        }
    }

    // A peer is in at most one instance, so any prior listing for this endpoint is stale on a new punch.
    private void RemoveEndpointEverywhere(IPEndPoint external)
    {
        foreach (var instance in byInstanceId.Values)
        {
            instance.PunchEndpoints.RemoveAll(e => e.External.Equals(external));
        }
    }
}
