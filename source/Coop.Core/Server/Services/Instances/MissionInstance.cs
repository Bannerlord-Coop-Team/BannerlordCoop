using LiteNetLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side record of a single P2P mission instance: the group of co-located players sharing one
/// settlement interior. The id is derived client-side from settlement + location; the server holds the
/// P2P socket endpoints each peer presents for NAT introduction, and — for the relay fallback — the
/// mapping from each member's controller id to its live server-side connection.
/// </summary>
internal class MissionInstance
{
    public string Id { get; }

    /// <summary>
    /// P2P socket endpoints presented via NAT-introduction requests.
    /// </summary>
    public List<Endpoints> PunchEndpoints { get; } = new List<Endpoints>();

    // Relay-fallback routing table. When a direct NAT punch fails, members stay connected to the server
    // and exchange traffic through it. A RelayPacket names its recipients by controller id, so the server
    // resolves those ids to the live server-side NetPeer connections for THIS instance. Concurrent because
    // it is read on the network poll thread while the MissionManager mutates instances under its own lock.
    private readonly ConcurrentDictionary<string, NetPeer> controllerToPeer = new ConcurrentDictionary<string, NetPeer>();
    private readonly ConcurrentDictionary<NetPeer, string> peerToController = new ConcurrentDictionary<NetPeer, string>();

    public MissionInstance(string id)
    {
        Id = id;
    }

    /// <summary>Controller ids currently routed through this instance (relay-fallback membership).</summary>
    public IReadOnlyCollection<string> Controllers => controllerToPeer.Keys.ToArray();

    /// <summary>
    /// Associate a member's controller id with its live server connection. Overwrites any prior mapping
    /// for the id so a re-joining controller (new NetPeer, same id) replaces its stale entry instead of
    /// being ignored.
    /// </summary>
    public void MapPeer(string controllerId, NetPeer peer)
    {
        if (controllerToPeer.TryGetValue(controllerId, out var previous) && previous != peer)
        {
            peerToController.TryRemove(previous, out _);
        }

        controllerToPeer[controllerId] = peer;
        peerToController[peer] = controllerId;
    }

    /// <summary>Drop a member by its connection (e.g. on disconnect).</summary>
    public void RemovePeer(NetPeer peer)
    {
        if (peerToController.TryRemove(peer, out var controllerId))
        {
            controllerToPeer.TryRemove(controllerId, out _);
        }
    }

    /// <summary>Resolve a single member's live connection.</summary>
    public bool TryGetPeer(string controllerId, out NetPeer peer) =>
        controllerToPeer.TryGetValue(controllerId, out peer);

    /// <summary>
    /// Resolve the live connections for a set of controller ids (a RelayPacket's recipients), skipping
    /// any id with no current mapping (e.g. a member that has already dropped).
    /// </summary>
    public IEnumerable<NetPeer> GetPeers(IEnumerable<string> controllerIds)
    {
        foreach (var controllerId in controllerIds)
        {
            if (controllerToPeer.TryGetValue(controllerId, out var peer))
            {
                yield return peer;
            }
        }
    }

    /// <summary>The internal (LAN) and external (WAN) endpoints a peer presents for NAT introduction.</summary>
    public readonly struct Endpoints
    {
        public readonly IPEndPoint Internal;
        public readonly IPEndPoint External;

        public Endpoints(IPEndPoint @internal, IPEndPoint external)
        {
            Internal = @internal;
            External = external;
        }
    }
}
