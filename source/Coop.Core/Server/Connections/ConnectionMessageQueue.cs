using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Server-side, per-peer gate that withholds world broadcasts from a client while it is still loading,
/// so it is not flooded with deltas it has no campaign to apply. Every <c>SendAll</c>/<c>SendAllBut</c>
/// runs through here per peer; single-peer handshake and save sends bypass it.
/// </summary>
/// <remarks>
/// Each peer's channel moves through three phases:
/// <list type="bullet">
/// <item><b>Dropping</b> (on <see cref="PlayerConnected"/>): pre-save broadcasts are discarded — they
/// are already in the save the peer is about to load.</item>
/// <item><b>Queueing</b> (on <see cref="BeginQueueing"/>, just after the save snapshot): broadcasts are
/// held FIFO — they are not in the save.</item>
/// <item><b>Open</b> (on <see cref="PlayerCampaignEntered"/>): held packets are replayed FIFO, the
/// channel is dropped, and the peer goes live (a peer with no channel is live).</item>
/// </list>
/// The drop/queue cut is clean: the save runs in a blocking <c>RunOnMainThread</c> on the network
/// thread, so the poller is parked and nothing races the snapshot. Replay-before-live is held by the
/// per-peer gate lock (across the whole flush, Open flipped last), not by thread identity or the
/// non-thread-safe broker.
/// </remarks>
public interface IConnectionMessageQueue
{
    /// <summary>
    /// Consulted for every broadcast to a single peer. Returns <c>true</c> when the queue has taken
    /// responsibility for the packet (dropped while pre-save, or held while loading) and the caller
    /// must NOT send it live; <c>false</c> when the caller should send it immediately.
    /// </summary>
    bool TryHandleBroadcast(NetPeer peer, IPacket packet);

    /// <summary>
    /// Moves a peer from <c>Dropping</c> to <c>Queueing</c>. Call on the main thread immediately after
    /// the transfer-save snapshot is taken. Because that save runs under a blocking RunOnMainThread
    /// call issued from the network thread the poller is parked, so the snapshot is not raced and this
    /// cut cleanly separates "in the save" (dropped) from "after the save" (queued for replay).
    /// </summary>
    void BeginQueueing(NetPeer peer);
}

/// <inheritdoc cref="IConnectionMessageQueue"/>
internal sealed class ConnectionMessageQueue : IConnectionMessageQueue, IDisposable
{
    private enum Phase
    {
        Dropping,
        Queueing,
        Open,
    }

    private sealed class PeerChannel
    {
        public readonly object Gate = new object();
        public Phase Phase = Phase.Dropping;
        public readonly Queue<IPacket> Pending = new Queue<IPacket>();
    }

    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionMessageQueue>();

    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this queue, and the
    // queue only needs INetwork later, at flush time, to replay held packets.
    private readonly Lazy<INetwork> network;
    private readonly IMessageBroker messageBroker;

    private readonly ConcurrentDictionary<NetPeer, PeerChannel> channels = new ConcurrentDictionary<NetPeer, PeerChannel>();

    public ConnectionMessageQueue(Lazy<INetwork> network, IMessageBroker messageBroker)
    {
        this.network = network;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public bool TryHandleBroadcast(NetPeer peer, IPacket packet)
    {
        // No channel means a fully-joined (or unknown) peer: send live.
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        lock (channel.Gate)
        {
            switch (channel.Phase)
            {
                case Phase.Queueing:
                    channel.Pending.Enqueue(packet);
                    return true;
                case Phase.Dropping:
                    // Already in the save the peer is about to load; discard.
                    return true;
                default:
                    // Open: reachable only in the brief race window between the flush flipping the
                    // channel and removing it from the dictionary (concurrency-only). Send live.
                    return false;
            }
        }
    }

    public void BeginQueueing(NetPeer peer)
    {
        // GetOrAdd guards the (not expected) case where BeginQueueing runs before PlayerConnected was
        // handled: the peer still ends up Queueing rather than silently receiving live broadcasts.
        var channel = channels.GetOrAdd(peer, _ => new PeerChannel());

        lock (channel.Gate)
        {
            channel.Phase = Phase.Queueing;
        }
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> payload)
    {
        channels.TryAdd(payload.What.PlayerPeer, new PeerChannel());
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        Flush(payload.What.playerId);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        // Idempotent: a peer that never connected, or a double disconnect, removes nothing.
        channels.TryRemove(payload.What.PlayerId, out _);
    }

    private void Flush(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return;

        int replayed;
        lock (channel.Gate)
        {
            replayed = channel.Pending.Count;

            // Replay under the gate so a broadcast racing the campaign-entered signal either lands in
            // Pending before this drain (and is replayed in order) or sees Open afterwards and goes
            // live strictly after the replay. Send only serializes + hands off to a non-blocking
            // LiteNetLib queue and never re-enters this gate.
            while (channel.Pending.Count > 0)
            {
                network.Value.Send(peer, channel.Pending.Dequeue());
            }

            channel.Phase = Phase.Open;
        }

        // Open already makes the channel behave as live; removal is just cleanup so steady-state
        // broadcasts skip the lookup. A concurrent broadcast that still holds the channel reference
        // observed Open above and was sent live.
        channels.TryRemove(peer, out _);

        Logger.Debug("Flushed {Count} queued packets to peer {Peer} on campaign entry", replayed, peer.Id);
    }
}
