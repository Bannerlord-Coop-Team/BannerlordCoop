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
