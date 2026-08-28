using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Players.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Server-side, per-peer gate that withholds world broadcasts while a client loads or views death statistics.
/// Every <c>SendAll</c>/<c>SendAllBut</c>
/// runs through here per peer; single-peer handshake and save sends bypass it.
/// </summary>
/// <remarks>
/// Each peer's channel uses these phases:
/// <list type="bullet">
/// <item><b>Dropping</b> (on <see cref="PlayerConnected"/>): pre-save broadcasts are discarded — they
/// are already in the save the peer is about to load.</item>
/// <item><b>Queueing</b> (on <see cref="BeginQueueing"/>, just after the save snapshot): broadcasts are
/// held FIFO — they are not in the save.</item>
/// <item><b>Open</b> (after the join barrier): held packets are replayed FIFO, a reliable tail marker
/// is appended, and later broadcasts pass through while the client applies the tail.</item>
/// <item><b>Live</b> (after <see cref="CompleteCatchUp"/>): the retained channel keeps passing broadcasts
/// through until disconnect.</item>
/// <item><b>Stopped</b> (on deletion): world updates are discarded until the peer disconnects.</item>
/// </list>
/// A peer with no channel is treated as newly accepted and its world broadcasts are dropped. LiteNetLib
/// exposes an accepted peer to fan-out before raising <see cref="PlayerConnected"/>, so treating an
/// unknown peer as live can deliver campaign objects before its save loads.
/// The drop/queue cut is clean: the save runs in a blocking <c>GameThread.Run</c> on the network
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
    /// the transfer-save snapshot is taken. Because that save runs under a blocking GameThread.Run
    /// call issued from the network thread the poller is parked, so the snapshot is not raced and this
    /// cut cleanly separates "in the save" (dropped) from "after the save" (queued for replay).
    /// </summary>
    void BeginQueueing(NetPeer peer);

    /// <summary>Replays held packets while keeping later broadcasts queued.</summary>
    void Flush(NetPeer peer);

    /// <summary>Gets the gate and reliable-channel backlog while catch-up is active.</summary>
    bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining);

    /// <summary>Replays held packets, appends a reliable marker, and opens the peer atomically.</summary>
    void OpenWithTail(NetPeer peer, IMessage tailMarker);

    /// <summary>Stops progress tracking after the client applies the ordered join tail.</summary>
    void CompleteCatchUp(NetPeer peer);
}

/// <inheritdoc cref="IConnectionMessageQueue"/>
internal sealed class ConnectionMessageQueue : IConnectionMessageQueue, IDisposable
{
    private enum Phase
    {
        Dropping,
        Queueing,
        Open,
        Live,
        Stopped,
    }

    private sealed class PeerChannel
    {
        public readonly object Gate = new object();
        public volatile Phase Phase = Phase.Dropping;
        public readonly Queue<IPacket> Pending = new Queue<IPacket>();
        public int PendingCount;
    }

    /// <summary>Keys connection generations by peer instance rather than reusable endpoint.</summary>
    private sealed class NetPeerReferenceComparer : IEqualityComparer<NetPeer>
    {
        public static readonly NetPeerReferenceComparer Instance = new NetPeerReferenceComparer();

        public bool Equals(NetPeer x, NetPeer y) => ReferenceEquals(x, y);
        public int GetHashCode(NetPeer peer) => RuntimeHelpers.GetHashCode(peer);
    }

    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionMessageQueue>();

    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this queue, and the
    // queue only needs INetwork later, at flush time, to replay held packets.
    private readonly Lazy<INetwork> network;
    private readonly IMessageBroker messageBroker;

    private readonly ConcurrentDictionary<NetPeer, PeerChannel> channels =
        new ConcurrentDictionary<NetPeer, PeerChannel>(NetPeerReferenceComparer.Instance);

    public ConnectionMessageQueue(Lazy<INetwork> network, IMessageBroker messageBroker)
    {
        this.network = network;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerDeletionStarted>(Handle_PlayerDeletionStarted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerDeletionStarted>(Handle_PlayerDeletionStarted);
    }

    public bool TryHandleBroadcast(NetPeer peer, IPacket packet)
    {
        if (ShouldBypassLoadingQueue(packet)) return false;

        // LiteNetLib exposes an accepted peer to SendAll before OnPeerConnected installs its channel.
        // Fail closed during that gap so world objects cannot reach a client before its transfer save.
        if (channels.TryGetValue(peer, out var channel) == false) return true;

        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Stopped) return true;
            if (ShouldBypassLoadingQueue(packet)) return false;

            switch (channel.Phase)
            {
                case Phase.Queueing:
                    channel.Pending.Enqueue(packet);
                    Interlocked.Increment(ref channel.PendingCount);
                    return true;
                case Phase.Dropping:
                    // Already in the save the peer is about to load; discard.
                    return true;
                default:
                    // Open and Live peers receive normal world traffic. Retaining the Live channel
                    // lets an absent channel unambiguously mean a newly accepted peer.
                    return false;
            }
        }
    }

    private static bool ShouldBypassLoadingQueue(IPacket packet)
    {
        // Campaign time is a periodic current-state sample, not history to replay after loading.
        if (packet.PacketType == PacketType.CampaignTime) return true;

        // Lobby membership is connection metadata, not campaign state contained in the save.
        return packet is MessagePacket messagePacket &&
               messagePacket.MessageType == typeof(NetworkSessionLobbyChanged);
    }

    private void Handle_PlayerDeletionStarted(MessagePayload<PlayerDeletionStarted> payload)
    {
        // Stop world replication for deleted player.
        // Game state at game over should be preserved for statistics screen when supported
        var channel = channels.GetOrAdd(payload.What.Peer, _ => new PeerChannel());
        lock (channel.Gate)
        {
            channel.Pending.Clear();
            Interlocked.Exchange(ref channel.PendingCount, 0);
            channel.Phase = Phase.Stopped;
        }
    }

    public void BeginQueueing(NetPeer peer)
    {
        // GetOrAdd guards the (not expected) case where BeginQueueing runs before PlayerConnected was
        // handled: the peer still ends up Queueing rather than silently receiving live broadcasts.
        var channel = channels.GetOrAdd(peer, _ => new PeerChannel());

        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Stopped) return;
            channel.Phase = Phase.Queueing;
        }
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> payload)
    {
        channels.TryAdd(payload.What.PlayerPeer, new PeerChannel());
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        // Idempotent: a peer that never connected, or a double disconnect, removes nothing.
        channels.TryRemove(payload.What.PlayerId, out _);
    }

    public void Flush(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return;

        int replayed;
        lock (channel.Gate)
        {
            replayed = channel.Pending.Count;
            Drain(peer, channel);
        }

        Logger.Debug("Flushed {Count} queued packets to peer {Peer} while joining", replayed, peer.Id);
    }

    public bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining)
    {
        packetsRemaining = 0;
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        if (channel.Phase == Phase.Dropping
            || channel.Phase == Phase.Live
            || channel.Phase == Phase.Stopped) return false;

        packetsRemaining = Volatile.Read(ref channel.PendingCount) +
                           peer.GetPacketsCountInReliableQueue(0, true) +
                           peer.GetPacketsCountInReliableQueue(0, false);
        return true;
    }

    public void OpenWithTail(NetPeer peer, IMessage tailMarker)
    {
        if (tailMarker == null) throw new ArgumentNullException(nameof(tailMarker));

        // Disconnect can remove the channel after the loading state checks that it is still current.
        if (channels.TryGetValue(peer, out var channel) == false) return;

        int replayed;
        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Stopped) return;
            replayed = channel.Pending.Count;
            Drain(peer, channel);

            // A racing broadcast is either drained before this marker or observes Open and is sent
            // after it. The client can therefore use the marker as the reliable world-stream barrier.
            network.Value.SendImmediate(peer, tailMarker);
            channel.Phase = Phase.Open;
        }

        Logger.Debug("Opened peer {Peer} after {Count} queued packets and the join tail marker", peer.Id, replayed);
    }

    public void CompleteCatchUp(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return;

        lock (channel.Gate)
        {
            if (channel.Phase != Phase.Open) return;
            channel.Phase = Phase.Live;
        }
    }

    private void Drain(NetPeer peer, PeerChannel channel)
    {
        while (channel.Pending.Count > 0)
        {
            var packet = channel.Pending.Dequeue();
            Interlocked.Decrement(ref channel.PendingCount);
            network.Value.SendImmediate(peer, packet);
        }
    }
}
