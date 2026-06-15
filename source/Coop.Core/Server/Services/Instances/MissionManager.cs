using Common.Logging;
using Common.Network.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Result of a peer joining an instance.
/// </summary>
public readonly struct JoinResult
{
    public readonly Guid InstanceId;
    public readonly bool BecameHost;

    public JoinResult(Guid instanceId, bool becameHost)
    {
        InstanceId = instanceId;
        BecameHost = becameHost;
    }
}

/// <summary>
/// Result of a peer leaving an instance. <see cref="NewHost"/> is non-null only when the leaving
/// peer was the host and other members remain (host was re-elected).
/// </summary>
public readonly struct LeaveResult
{
    public readonly bool WasMember;
    public readonly Guid InstanceId;
    public readonly NetPeer NewHost;

    public LeaveResult(bool wasMember, Guid instanceId, NetPeer newHost)
    {
        WasMember = wasMember;
        InstanceId = instanceId;
        NewHost = newHost;
    }
}

/// <summary>
/// Authoritative owner of server-side P2P mission instances. Maps a (settlement, location) pair to a
/// single server-issued <see cref="Guid"/> <see cref="MissionInstance"/>, tracks which peers are in
/// it, elects / re-elects the host, and acts as the co-hosted NAT-punch rendezvous that introduces
/// co-located peers so they can open direct connections. A peer is in at most one instance at a time.
/// </summary>
public interface IMissionManager
{
    /// <summary>Join (or create) the instance for a (settlement, location) and return its id + whether this peer became host.</summary>
    JoinResult Join(NetPeer peer, string settlementId, string locationId);

    /// <summary>Remove a peer from whatever instance it is in, re-electing the host if needed.</summary>
    LeaveResult Leave(NetPeer peer);

    /// <summary>
    /// Handle a NAT-introduction request: introduce the requesting peer to every other peer already
    /// punched into the same instance. Driven purely by the request's <see cref="ConnectionToken"/>,
    /// whose instance name is the server-issued instance id.
    /// </summary>
    void HandleIntroductionRequest(NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
}

/// <inheritdoc cref="IMissionManager"/>
public class MissionManager : IMissionManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionManager>();

    // NetPeer compares by endpoint, not identity; key peers by reference so two distinct peers that
    // happen to share an endpoint (e.g. behind one NAT, or in tests) are never conflated.
    private sealed class PeerRefComparer : IEqualityComparer<NetPeer>
    {
        public static readonly PeerRefComparer Instance = new PeerRefComparer();
        public bool Equals(NetPeer x, NetPeer y) => ReferenceEquals(x, y);
        public int GetHashCode(NetPeer obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly object gate = new object();
    private readonly Dictionary<string, MissionInstance> byLocation = new Dictionary<string, MissionInstance>();
    private readonly Dictionary<string, MissionInstance> byInstanceId = new Dictionary<string, MissionInstance>();
    private readonly Dictionary<NetPeer, MissionInstance> byPeer = new Dictionary<NetPeer, MissionInstance>(PeerRefComparer.Instance);

    private static string LocationKey(string settlementId, string locationId) => settlementId + "|" + locationId;

    public JoinResult Join(NetPeer peer, string settlementId, string locationId)
    {
        lock (gate)
        {
            var key = LocationKey(settlementId, locationId);

            // Idempotent re-entry: entering a location fires PlayerEnteredLocation several times
            // (OpenIndoorMission runs more than once per entry), so the same peer requests the same
            // location repeatedly. Without this guard, the LeaveInternal below would retire the
            // just-created instance (the peer is its only member) and mint a fresh id every time —
            // invalidating the NAT punch the client already sent, which the server then drops as an
            // "unknown instance". Keep the peer in its current instance instead.
            if (byPeer.TryGetValue(peer, out var existing) &&
                LocationKey(existing.SettlementId, existing.LocationId) == key)
            {
                return new JoinResult(existing.Id, ReferenceEquals(existing.Host, peer));
            }

            // A peer can only be in one instance; drop it from any previous (different) one first.
            LeaveInternal(peer);

            if (byLocation.TryGetValue(key, out var instance) == false)
            {
                instance = new MissionInstance(Guid.NewGuid(), settlementId, locationId);
                byLocation[key] = instance;
                byInstanceId[instance.Id.ToString()] = instance;
                Logger.Information("Created instance {InstanceId} for {SettlementId}/{LocationId}",
                    instance.Id, settlementId, locationId);
            }

            bool becameHost = instance.Members.Count == 0;
            instance.Members.Add(peer);
            byPeer[peer] = instance;

            if (becameHost)
            {
                instance.Host = peer;
            }

            Logger.Debug("Peer joined instance {InstanceId} ({Count} members, host={IsHost})",
                instance.Id, instance.Members.Count, becameHost);

            return new JoinResult(instance.Id, becameHost);
        }
    }

    public LeaveResult Leave(NetPeer peer)
    {
        lock (gate)
        {
            return LeaveInternal(peer);
        }
    }

    public void HandleIntroductionRequest(
        NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            Logger.Warning("Discarding NAT introduction with unparseable token from {Endpoint}", remoteEndPoint);
            return;
        }

        string instanceId = connectionToken.InstanceName;

        lock (gate)
        {
            // The client only learns the instance id after the server assigns it (NetworkAssignInstance),
            // so the instance must already exist. A punch for an unknown instance is stale or spoofed.
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                Logger.Warning("Discarding NAT introduction for unknown instance {Instance} from {Endpoint}",
                    instanceId, remoteEndPoint);
                return;
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

    // Must be called under the lock.
    private LeaveResult LeaveInternal(NetPeer peer)
    {
        if (byPeer.TryGetValue(peer, out var instance) == false)
        {
            return new LeaveResult(wasMember: false, default, null);
        }

        byPeer.Remove(peer);
        instance.Members.RemoveAll(p => ReferenceEquals(p, peer));

        if (instance.Members.Count == 0)
        {
            // Retiring the instance also drops its accumulated punch endpoints.
            byLocation.Remove(LocationKey(instance.SettlementId, instance.LocationId));
            byInstanceId.Remove(instance.Id.ToString());
            Logger.Information("Retired empty instance {InstanceId}", instance.Id);
            return new LeaveResult(wasMember: true, instance.Id, newHost: null);
        }

        NetPeer newHost = null;
        if (ReferenceEquals(instance.Host, peer))
        {
            // Re-elect: first remaining member becomes host.
            newHost = instance.Members[0];
            instance.Host = newHost;
            Logger.Information("Re-elected host for instance {InstanceId}", instance.Id);
        }

        return new LeaveResult(wasMember: true, instance.Id, newHost);
    }

    // A peer is in at most one instance, so any prior listing for this endpoint is stale on a new punch.
    private void RemoveEndpointEverywhere(IPEndPoint external)
    {
        foreach (var instance in byLocation.Values)
        {
            instance.PunchEndpoints.RemoveAll(e => e.External.Equals(external));
        }
    }
}
