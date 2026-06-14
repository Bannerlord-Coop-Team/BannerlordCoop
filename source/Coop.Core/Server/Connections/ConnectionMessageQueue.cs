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
/// Server-side, per-peer broadcast queue that stops a still-loading client from receiving the live
/// world stream before it can apply it. A peer is transport-connected the instant it joins — long
/// before it has the host save — yet every <c>SendAll</c>/<c>SendAllBut</c> fans out to every
/// connected peer, so without this gate a joining client is flooded with deltas it has no campaign to
/// apply them against.
/// </summary>
/// <remarks>
/// Each peer moves through three phases:
/// <list type="bullet">
/// <item><b>Dropping</b> (default, created on <see cref="PlayerConnected"/>): the peer has connected
/// but its transfer save has not been taken yet. Broadcasts are discarded — their world change is
/// already captured by the save the peer is about to receive, so replaying them would be redundant
/// (and for additive messages divergent).</item>
/// <item><b>Queueing</b> (entered via <see cref="BeginQueueing"/>, called on the main thread just
/// after the save snapshot): broadcasts are held in FIFO order — they carry world changes the save
/// does not contain, so the peer must receive them once it is live.</item>
/// <item><b>Open</b> (reached when the peer reports <see cref="PlayerCampaignEntered"/>): the held
/// packets are replayed in FIFO order and the channel is dropped, after which the peer receives
/// broadcasts live like any fully-joined peer.</item>
/// </list>
/// A peer with no channel — never connected, or already flushed — is treated as live: broadcasts are
/// sent immediately. Only fan-out broadcasts pass through here; single-peer handshake and save sends
/// (<see cref="GameInterface.Services.Heroes.Interfaces.ISaveInterface"/> transfer, the new-hero
/// reply) use <see cref="INetwork.Send(NetPeer, IPacket)"/> and bypass the queue by construction, so
/// no packet-type whitelist is needed.
///
/// The Dropping/Queueing cut is taken at the save boundary and is effectively atomic. The transfer
/// save runs inside a blocking <c>GameLoopRunner.RunOnMainThread</c> call issued from the network
/// thread, so the poller is parked for the save's duration and cannot broadcast a received delta that
/// races the snapshot, while the main thread takes the snapshot with no other game logic interleaved.
/// <see cref="BeginQueueing"/> is called immediately after the snapshot, so a broadcast is dropped iff
/// its world change is already in the save and queued iff it is not.
///
/// Ordering and memory visibility rest solely on the per-peer gate lock — never on thread identity
/// (the network poller is a rotating thread-pool set) nor on broker delivery order (the message broker
/// is not thread-safe and broadcasts fire from both the main and poller threads). The gate is held
/// across the entire flush, flipping the channel out of reach as its last action, so replayed packets
/// always precede any live broadcast that races the campaign-entered signal.
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
