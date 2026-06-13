using Common.Messaging;
using Common.Network.Messages;
using LiteNetLib;
using System;
using System.Collections.Concurrent;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Tracks which peers may receive world broadcasts. A joining peer cannot use any world state
/// until it has the transfer save — everything broadcast before the snapshot is already inside
/// it — so broadcasts are withheld from the moment a peer connects until the save has been
/// enqueued to it. Join handshake traffic is unaffected: it is sent peer-targeted, never
/// broadcast.
/// </summary>
/// <remarks>
/// Without this, the full world-sync stream floods a peer that is still validating modules or
/// creating a character: its send queue overloads (pausing the world for everyone), and the
/// client applies world messages against a campaign it has not loaded yet.
///
/// The gate opens inside the same main-thread block that snapshots and sends the save, and the
/// save rides the same reliable-ordered channel as message broadcasts, so no ordered world
/// broadcast can overtake the save into a newly opened peer's queue. Two narrow windows remain
/// and are tolerated: broadcasts produced off the main thread (periodic time sync, info
/// messages) can race the opening and skip the peer once — both periodic or cosmetic — and
/// reliable-UNORDERED packets (party behavior) enqueued after the save may still arrive ahead
/// of it, relying on their lookup-guarded handlers. A peer that disconnects during the snapshot
/// can leave one stale entry behind (the disconnect was processed before the open); it is
/// unreachable for sending and is reclaimed when the session container is rebuilt.
/// </remarks>
public interface IPeerBroadcastGate
{
    /// <summary>True once the transfer save has been enqueued to the peer.</summary>
    bool CanBroadcastTo(NetPeer peer);

    /// <summary>Allows world broadcasts to the peer from this point on.</summary>
    void Open(NetPeer peer);
}

internal sealed class PeerBroadcastGate : IPeerBroadcastGate, IDisposable
{
    private readonly IMessageBroker messageBroker;
    private readonly ConcurrentDictionary<NetPeer, byte> openPeers = new();

    public PeerBroadcastGate(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public bool CanBroadcastTo(NetPeer peer) => openPeers.ContainsKey(peer);

    public void Open(NetPeer peer) => openPeers.TryAdd(peer, 0);

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        openPeers.TryRemove(payload.What.PlayerId, out _);
    }
}
