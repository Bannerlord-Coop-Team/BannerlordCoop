using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Server-side, per-peer gate that withholds world broadcasts from a client while it is still loading,
/// so it is not flooded with deltas it has no campaign to apply. Every <c>SendAll</c>/<c>SendAllBut</c>
/// runs through here per peer; single-peer handshake and save sends bypass it.
/// </summary>
/// <remarks>
/// Each peer's channel moves through four phases:
/// <list type="bullet">
/// <item><b>Dropping</b> (on <see cref="PlayerConnected"/>): pre-save broadcasts are discarded — they
/// are already in the save the peer is about to load.</item>
/// <item><b>Queueing</b> (on <see cref="BeginQueueing"/>, just after the save snapshot): broadcasts are
/// held FIFO — they are not in the save.</item>
/// <item><b>Open</b> (after the join barrier): held packets are replayed FIFO, a reliable tail marker
/// is appended, and later broadcasts pass through while the client applies the tail.</item>
/// <item><b>Live</b> (after <see cref="CompleteCatchUp"/>): the retained channel keeps passing broadcasts
/// through until disconnect.</item>
/// </list>
/// Unknown peers drop world broadcasts: LiteNetLib can expose an accepted peer to fan-out before
/// raising <see cref="PlayerConnected"/>. Campaign time and lobby membership bypass this gate.
/// The drop/queue cut is clean: the save runs in a blocking <c>GameThread.Run</c> on the network
/// thread while the network poller waits for the save capture to finish.
/// Replay-before-live is held by the per-peer gate lock (across the whole flush, Open flipped last),
/// not by thread identity or the non-thread-safe broker.
/// </remarks>
public interface IConnectionMessageQueue
{
    /// <summary>
    /// Isolates a newly accepted peer before connection listeners can emit campaign traffic for it.
    /// Idempotent with the normal <see cref="PlayerConnected"/> handler.
    /// </summary>
    void RegisterPeer(NetPeer peer);

    /// <summary>
    /// Consulted for every broadcast to a single peer. Returns <c>true</c> when the queue has taken
    /// responsibility for the packet (dropped while pre-save, or held while loading) and the caller
    /// must NOT send it live; <c>false</c> when the caller should send it immediately.
    /// </summary>
    bool TryHandleBroadcast(NetPeer peer, IPacket packet);

    /// <summary>
    /// Moves a peer from <c>Dropping</c> to <c>Queueing</c>. Call on the main thread immediately after
    /// the transfer-save snapshot is taken. Because that save runs under a blocking GameThread.Run
    /// call issued from the network thread the poller is parked, so world-history sends stay behind the
    /// capture and this cut cleanly separates "in the save" (dropped) from "after the save" (queued).
    /// </summary>
    void BeginQueueing(NetPeer peer);

    /// <summary>Replays one bounded batch while keeping later broadcasts queued.</summary>
    JoinReplayBatchResult FlushBatch(NetPeer peer);

    /// <summary>Defers an oversized singleton while earlier batches still await application.</summary>
    JoinReplayBatchResult FlushBatch(NetPeer peer, bool allowOversized);

    /// <summary>
    /// Ends the period where current-state broadcasts are covered by the final authoritative
    /// campaign baseline. Call immediately before that baseline is captured; later changes must
    /// remain queued behind it.
    /// </summary>
    void EndFinalBaselineCoverage(NetPeer peer);

    /// <summary>Gets the gate and reliable-channel backlog while catch-up is active.</summary>
    bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining);

    /// <summary>Gets serialized replay bytes still retained by the gate.</summary>
    bool TryGetCatchUpPendingBytes(NetPeer peer, out long pendingBytes);

    /// <summary>Returns whether replay admission stopped before a configured safety limit.</summary>
    bool HasCatchUpOverflowed(NetPeer peer);

    /// <summary>Returns a compact queue breakdown for join diagnostics.</summary>
    string DescribeCatchUp(NetPeer peer);

    /// <summary>
    /// Replays one bounded final batch. When it empties the gate, begins the terminal phase, appends
    /// a reliable marker, and opens the peer atomically.
    /// </summary>
    JoinReplayBatchResult OpenWithTailBatch(
        NetPeer peer,
        IMessage tailMarker,
        Func<bool> tryBeginOpen);

    /// <summary>Stops progress tracking after the client applies the ordered join tail.</summary>
    void CompleteCatchUp(NetPeer peer);

    /// <summary>Clears a failed join's replay and keeps later broadcasts isolated until disconnect.</summary>
    int AbortCatchUp(NetPeer peer);
}

/// <summary>Progress and admission status of one bounded replay batch.</summary>
public readonly struct JoinReplayBatchResult
{
    public readonly int PacketsSent;
    public readonly int BytesSent;
    public readonly bool HasMore;
    public readonly bool Overflowed;

    public JoinReplayBatchResult(
        int packetsSent,
        int bytesSent,
        bool hasMore,
        bool overflowed = false)
    {
        PacketsSent = packetsSent;
        BytesSent = bytesSent;
        HasMore = hasMore;
        Overflowed = overflowed;
    }
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
    }

    private sealed class PeerChannel
    {
        public readonly object Gate = new object();
        public volatile Phase Phase = Phase.Dropping;
        public readonly LinkedList<IPacket> Pending = new LinkedList<IPacket>();
        public LinkedListNode<IPacket> PendingMerge;
        public int PendingCount;
        public long PendingBytes;
        public bool Overflowed;
        public bool FinalBaselineCoverageActive = true;
        public long MergedAdjacentTotal;
    }

    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionMessageQueue>();
    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this queue, and the
    // queue only needs INetwork later, at flush time, to replay held packets.
    private readonly Lazy<INetwork> network;
    private readonly IMessageBroker messageBroker;
    private readonly ICommonSerializer serializer;
    private readonly int maxPendingPackets;
    private readonly long maxPendingBytes;

    private readonly ConcurrentDictionary<NetPeer, PeerChannel> channels =
        new ConcurrentDictionary<NetPeer, PeerChannel>(ReferenceComparer<NetPeer>.Instance);
    public ConnectionMessageQueue(
        Lazy<INetwork> network,
        IMessageBroker messageBroker,
        ICommonSerializer serializer)
        : this(
            network,
            messageBroker,
            serializer,
            NetworkJoinLimits.MaxReplayPackets,
            NetworkJoinLimits.MaxReplayPendingBytes)
    {
    }

    internal ConnectionMessageQueue(
        Lazy<INetwork> network,
        IMessageBroker messageBroker,
        ICommonSerializer serializer,
        int maxPendingPackets,
        long maxPendingBytes)
    {
        if (maxPendingPackets <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingPackets));
        if (maxPendingBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingBytes));

        this.network = network;
        this.messageBroker = messageBroker;
        this.serializer = serializer;
        this.maxPendingPackets = maxPendingPackets;
        this.maxPendingBytes = maxPendingBytes;

        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public bool TryHandleBroadcast(NetPeer peer, IPacket packet)
    {
        if (packet.PacketType == PacketType.CampaignTime ||
            packet is MessagePacket lobby && lobby.MessageType == typeof(NetworkSessionLobbyChanged))
            return false;

        if (!channels.TryGetValue(peer, out var channel)) return true;
        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Dropping) return true;
            if (channel.Phase != Phase.Queueing) return false;

            var mergeNode = channel.PendingMerge;
            channel.PendingMerge = null;
            if (channel.Overflowed) return true;

            int bytes = GetReplayPacketSize(packet);
            MessagePacket combined = default;
            bool merged = channel.FinalBaselineCoverageActive && mergeNode != null &&
                mergeNode.Value is MessagePacket previous && packet is MessagePacket next &&
                previous.TryMergeForJoinCatchUp(next, serializer, out combined);
            // Merge only the adjacent frozen payload; intervening world events are barriers.
            if (merged)
            {
                bytes = combined.Data.Length - ((MessagePacket)mergeNode.Value).Data.Length;
                packet = combined;
            }

            if ((!merged && channel.PendingCount >= maxPendingPackets) ||
                channel.PendingBytes + bytes > maxPendingBytes)
            {
                channel.Overflowed = true;
                return true;
            }

            LinkedListNode<IPacket> node;
            if (merged)
            {
                node = mergeNode;
                node.Value = packet;
                channel.MergedAdjacentTotal++;
            }
            else
            {
                node = channel.Pending.AddLast(packet);
                Interlocked.Increment(ref channel.PendingCount);
            }
            Interlocked.Add(ref channel.PendingBytes, bytes);
            if (packet is MessagePacket candidate && !string.IsNullOrEmpty(candidate.JoinCatchUpMergeKey))
                channel.PendingMerge = node;
            return true;
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
            channel.PendingMerge = null;
            channel.FinalBaselineCoverageActive = true;
            channel.Overflowed = false;
        }
    }

    public void RegisterPeer(NetPeer peer)
    {
        if (peer == null) throw new ArgumentNullException(nameof(peer));
        channels.TryAdd(peer, new PeerChannel());
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> payload)
    {
        RegisterPeer(payload.What.PlayerPeer);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        // Idempotent: a peer that never connected, or a double disconnect, removes nothing.
        channels.TryRemove(payload.What.PlayerId, out _);
    }

    public JoinReplayBatchResult FlushBatch(NetPeer peer) => FlushBatch(peer, allowOversized: true);

    public JoinReplayBatchResult FlushBatch(NetPeer peer, bool allowOversized)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return default;

        JoinReplayBatchResult result;
        lock (channel.Gate)
        {
            result = DrainBatch(peer, channel, allowOversized: allowOversized);
        }

        Logger.Debug(
            "Flushed replay batch to peer {Peer}: packets={Packets} bytes={Bytes} more={HasMore}",
            peer.Id,
            result.PacketsSent,
            result.BytesSent,
            result.HasMore);
        return result;
    }

    public void EndFinalBaselineCoverage(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return;

        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Queueing)
            {
                channel.FinalBaselineCoverageActive = false;
                channel.PendingMerge = null;
            }
        }
    }

    public bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining)
    {
        packetsRemaining = 0;
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        if (channel.Phase is Phase.Dropping or Phase.Live) return false;

        packetsRemaining = Volatile.Read(ref channel.PendingCount) +
                           peer.GetPacketsCountInReliableQueue(0, true) +
                           peer.GetPacketsCountInReliableQueue(0, false);
        return true;
    }

    public bool TryGetCatchUpPendingBytes(NetPeer peer, out long pendingBytes)
    {
        pendingBytes = 0;
        if (channels.TryGetValue(peer, out var channel) == false) return false;
        if (channel.Phase is Phase.Dropping or Phase.Live) return false;

        pendingBytes = Interlocked.Read(ref channel.PendingBytes);
        return true;
    }

    public bool HasCatchUpOverflowed(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        lock (channel.Gate)
        {
            return channel.Overflowed;
        }
    }

    public string DescribeCatchUp(NetPeer peer)
    {
        if (!channels.TryGetValue(peer, out var channel)) return "gate=none";
        lock (channel.Gate)
        {
            return $"gate={channel.Phase} pending={channel.PendingCount} " +
                $"pendingBytes={channel.PendingBytes} overflowed={channel.Overflowed} " +
                $"mergedAdjacent={channel.MergedAdjacentTotal}";
        }
    }

    public JoinReplayBatchResult OpenWithTailBatch(
        NetPeer peer,
        IMessage tailMarker,
        Func<bool> tryBeginOpen)
    {
        if (tailMarker == null) throw new ArgumentNullException(nameof(tailMarker));
        if (tryBeginOpen == null) throw new ArgumentNullException(nameof(tryBeginOpen));

        if (!channels.TryGetValue(peer, out var channel)) return default;

        JoinReplayBatchResult result;
        lock (channel.Gate)
        {
            result = DrainBatch(peer, channel);
            if (result.Overflowed) return result;
            if (result.HasMore) return result;
            if (!tryBeginOpen()) return result;

            // A racing broadcast is either drained before this marker or observes Open and is sent
            // after it. The client can therefore use the marker as the reliable world-stream barrier.
            network.Value.SendImmediate(peer, tailMarker);
            channel.Phase = Phase.Open;
        }

        Logger.Debug(
            "Opened peer {Peer} after final replay batch packets={Packets} bytes={Bytes}",
            peer.Id,
            result.PacketsSent,
            result.BytesSent);
        return result;
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

    public int AbortCatchUp(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false)
        {
            (network.Value as IBufferedNetwork)?.DiscardPendingMessages(peer);
            return 0;
        }

        int cleared;
        lock (channel.Gate)
        {
            cleared = channel.Pending.Count;
            channel.Pending.Clear();
            channel.PendingMerge = null;
            Interlocked.Exchange(ref channel.PendingCount, 0);
            Interlocked.Exchange(ref channel.PendingBytes, 0);
            channel.Overflowed = false;
            channel.Phase = Phase.Dropping;
            channel.FinalBaselineCoverageActive = true;
        }

        // Replay Drain uses normal message aggregation. Remove anything it buffered before the
        // watchdog was able to abort so the end-of-update aggregate flush cannot send stale replay.
        (network.Value as IBufferedNetwork)?.DiscardPendingMessages(peer);
        return cleared;
    }

    private JoinReplayBatchResult DrainBatch(
        NetPeer peer,
        PeerChannel channel,
        bool allowOversized = true)
    {
        int drained = 0;
        int drainedBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        while (channel.Pending.Count > 0 &&
               drained < NetworkJoinLimits.MaxReplayBatchPackets &&
               (drained == 0 || stopwatch.Elapsed < NetworkJoinLimits.ReplayBatchSendBudget))
        {
            LinkedListNode<IPacket> node = channel.Pending.First;
            var packet = node.Value;
            int packetBytes = GetReplayPacketSize(packet);
            if ((drained > 0 || !allowOversized) &&
                drainedBytes + packetBytes > NetworkJoinLimits.MaxReplayBatchBytes)
            {
                break;
            }

            channel.Pending.RemoveFirst();
            if (ReferenceEquals(node, channel.PendingMerge)) channel.PendingMerge = null;
            drained++;
            drainedBytes += packetBytes;
            Interlocked.Decrement(ref channel.PendingCount);
            Interlocked.Add(ref channel.PendingBytes, -packetBytes);
            network.Value.SendImmediate(peer, packet);
        }

        bool hasMore = channel.Pending.Count > 0;
        return new JoinReplayBatchResult(
            drained,
            drainedBytes,
            hasMore,
            channel.Overflowed);
    }

    private int GetReplayPacketSize(IPacket packet)
    {
        if (packet is MessagePacket messagePacket && messagePacket.Data != null)
            return messagePacket.Data.Length;

        return serializer.Serialize(packet).Length;
    }
}
