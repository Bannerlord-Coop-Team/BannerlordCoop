using Common.Messaging;
using Common.Network.Messages;
using LiteNetLib;
using System;
using System.Collections.Concurrent;

namespace Coop.Core.Server.Connections;

/// <summary>
/// The set of peers that may receive world broadcasts. <c>SendAll</c> broadcasts to every
/// transport-connected peer, but a peer is connected the moment it joins — long before it has
/// loaded the host save — so the broadcast loop simply skips any peer that is not in this set.
/// This is a skip, not a buffer: nothing is queued or dropped, the peer is passed over until it
/// has the save, at which point <see cref="TransferSaveState"/> adds it. Join handshake traffic
/// is unaffected — it is sent peer-targeted, never broadcast.
/// </summary>
/// <remarks>
/// There is nothing worth sending a peer before its save anyway: everything broadcast before the
/// snapshot is already inside it. Sending it regardless is actively harmful — the stream floods a
/// peer that is still validating modules or creating a character, overloading its send queue
/// (which pauses the world for everyone) and making the client apply world messages against a
/// campaign it has not loaded yet.
///
/// The peer is added inside the same main-thread block that snapshots and sends the save, right
/// after the save is sent, and the save rides the same reliable-ordered channel as message
/// broadcasts — so no ordered broadcast can overtake the save into a freshly added peer's queue.
/// Two edges are tolerated: a broadcast produced off the main thread (periodic time sync, info
/// messages) can race the add and skip the peer one extra time — both are periodic or cosmetic —
/// and reliable-UNORDERED packets (party behavior) sent after the save may still arrive ahead of
/// it, where their lookup-guarded handlers cover it. A peer that disconnects mid-snapshot can
/// leave one stale entry (the disconnect was handled before the add); it is unreachable for
/// sending and is reclaimed when the session container is rebuilt.
/// </remarks>
public interface IPeerBroadcastGate
{
    /// <summary>True once the peer has been sent the save and may receive world broadcasts.</summary>
    bool CanBroadcastTo(NetPeer peer);

    /// <summary>Adds the peer to the broadcast set; call once its save has been enqueued.</summary>
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
