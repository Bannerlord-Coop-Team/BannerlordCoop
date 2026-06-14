using Common.Logging;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
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
/// Authoritative owner of instance identity and membership. Maps a (settlement, location) pair to
/// a single server-issued <see cref="Guid"/> instance, tracks which peers are in it, and elects /
/// re-elects the host. A peer is in at most one instance at a time.
/// </summary>
public interface IInstanceCoordinator
{
    /// <summary>Join (or create) the instance for a (settlement, location) and return its id + whether this peer became host.</summary>
    JoinResult Join(NetPeer peer, string settlementId, string locationId);

    /// <summary>Remove a peer from whatever instance it is in, re-electing the host if needed.</summary>
    LeaveResult Leave(NetPeer peer);
}

/// <inheritdoc cref="IInstanceCoordinator"/>
public class InstanceCoordinator : IInstanceCoordinator
{
    private static readonly ILogger Logger = LogManager.GetLogger<InstanceCoordinator>();

    // NetPeer compares by endpoint, not identity; key peers by reference so two distinct peers that
    // happen to share an endpoint (e.g. behind one NAT, or in tests) are never conflated.
    private sealed class PeerRefComparer : IEqualityComparer<NetPeer>
    {
        public static readonly PeerRefComparer Instance = new PeerRefComparer();
        public bool Equals(NetPeer x, NetPeer y) => ReferenceEquals(x, y);
        public int GetHashCode(NetPeer obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly object gate = new object();
    private readonly Dictionary<string, Instance> byLocation = new Dictionary<string, Instance>();
    private readonly Dictionary<NetPeer, Instance> byPeer = new Dictionary<NetPeer, Instance>(PeerRefComparer.Instance);

    private static string LocationKey(string settlementId, string locationId) => settlementId + "|" + locationId;

    public JoinResult Join(NetPeer peer, string settlementId, string locationId)
    {
        lock (gate)
        {
            // A peer can only be in one instance; drop it from any previous one first.
            LeaveInternal(peer);

            var key = LocationKey(settlementId, locationId);
            if (byLocation.TryGetValue(key, out var instance) == false)
            {
                instance = new Instance(Guid.NewGuid(), settlementId, locationId);
                byLocation[key] = instance;
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
            byLocation.Remove(LocationKey(instance.SettlementId, instance.LocationId));
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
}
