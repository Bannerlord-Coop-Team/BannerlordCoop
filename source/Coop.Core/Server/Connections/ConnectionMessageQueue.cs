using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
/// <item><b>Open</b> (after the join barrier): held packets are replayed FIFO, a reliable tail marker
/// is appended, and the peer goes live (a peer with no channel is live).</item>
/// </list>
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

    void ExecuteWorldSend(IPacket packet, Action send);

    void ExecuteCacheNeutralWorldSend(Action send);

    bool TryUseCachedJoinSnapshot(NetPeer peer, out CachedJoinSnapshot snapshot);

    long CaptureFreshJoinSnapshot(
        NetPeer peer,
        Func<GameSaveDataPacket> capture,
        out GameSaveDataPacket snapshot);

    void CompleteJoinSnapshot(
        long generation,
        GameSaveDataPacket snapshot,
        byte[] compressedData);

    void InvalidateJoinSnapshot();

    /// <summary>
    /// Moves a peer from <c>Dropping</c> to <c>Queueing</c>. Call on the main thread immediately after
    /// the transfer-save snapshot is taken. Because that save runs under a blocking GameThread.Run
    /// call issued from the network thread the poller is parked, so the snapshot is not raced and this
    /// cut cleanly separates "in the save" (dropped) from "after the save" (queued for replay).
    /// </summary>
    void BeginQueueing(NetPeer peer);

    /// <summary>Replays held packets while keeping later broadcasts queued.</summary>
    void Flush(NetPeer peer);

    /// <summary>
    /// Ends the period where party-behavior broadcasts are covered by the final authoritative party
    /// baseline. Call after flushing the behavior coalescer and immediately before that baseline is
    /// captured; later behavior changes must remain queued behind it.
    /// </summary>
    void EndPartyBehaviorBaselineCoverage(NetPeer peer);

    /// <summary>Gets the gate and reliable-channel backlog while catch-up is active.</summary>
    bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining);

    /// <summary>Returns a compact queue breakdown for join diagnostics.</summary>
    string DescribeCatchUp(NetPeer peer);

    /// <summary>Replays held packets, appends a reliable marker, and opens the peer atomically.</summary>
    void OpenWithTail(NetPeer peer, IMessage tailMarker);

    /// <summary>Stops progress tracking after the client applies the ordered join tail.</summary>
    void CompleteCatchUp(NetPeer peer);
}

public readonly struct CachedJoinSnapshot
{
    public readonly GameSaveDataPacket Metadata;
    public readonly byte[] CompressedData;
    public readonly int UncompressedSize;

    public CachedJoinSnapshot(
        GameSaveDataPacket metadata,
        byte[] compressedData,
        int uncompressedSize)
    {
        Metadata = metadata;
        CompressedData = compressedData;
        UncompressedSize = uncompressedSize;
    }
}

/// <inheritdoc cref="IConnectionMessageQueue"/>
internal sealed class ConnectionMessageQueue : IConnectionMessageQueue, IDisposable
{
    private sealed class JoinSnapshotCacheEntry
    {
        public long Generation;
        public DateTime CutUtc;
        public GameSaveDataPacket Metadata;
        public byte[] CompressedData;
        public int UncompressedSize;
    }

    private enum Phase
    {
        Dropping,
        Queueing,
        Open,
    }

    private sealed class PeerChannel
    {
        public readonly object Gate = new object();
        public volatile Phase Phase = Phase.Dropping;
        public readonly Queue<IPacket> Pending = new Queue<IPacket>();
        public readonly Dictionary<string, int> PendingByType = new Dictionary<string, int>();
        public int PendingCount;
        public long EnqueuedTotal;
        public long DrainedTotal;
        public int DrainCount;
        public string LastDrain = "none";
        public bool PartyBehaviorCoveredByFinalBaseline = true;
        public long CoveredPartyBehaviorTotal;
    }

    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionMessageQueue>();
    private static readonly TimeSpan JoinSnapshotLifetime = TimeSpan.FromSeconds(5);
    [ThreadStatic] private static int cacheNeutralSendDepth;

    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this queue, and the
    // queue only needs INetwork later, at flush time, to replay held packets.
    private readonly Lazy<INetwork> network;
    private readonly IMessageBroker messageBroker;

    private readonly ConcurrentDictionary<NetPeer, PeerChannel> channels = new ConcurrentDictionary<NetPeer, PeerChannel>();
    private readonly object snapshotGate = new object();
    private JoinSnapshotCacheEntry joinSnapshot;
    private long joinSnapshotGeneration;

    public ConnectionMessageQueue(Lazy<INetwork> network, IMessageBroker messageBroker)
    {
        this.network = network;
        this.messageBroker = messageBroker;

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
        if (ShouldBypassLoadingQueue(packet)) return false;

        // No channel means a fully-joined (or unknown) peer: send live.
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        bool handled;
        lock (channel.Gate)
        {
            switch (channel.Phase)
            {
                case Phase.Queueing:
                    // The final join baseline is a complete authoritative snapshot of every mobile
                    // party's behavior. Sending the thousands of movement snapshots produced while
                    // the client loads is redundant. Once that final baseline is about to be captured,
                    // coverage ends and later changes stay in the ordered tail as normal.
                    if (channel.PartyBehaviorCoveredByFinalBaseline &&
                        IsPartyBehaviorSnapshot(packet))
                    {
                        channel.CoveredPartyBehaviorTotal++;
                        handled = true;
                        break;
                    }

                    channel.Pending.Enqueue(packet);
                    Increment(channel.PendingByType, GetPacketTypeName(packet));
                    channel.EnqueuedTotal++;
                    Interlocked.Increment(ref channel.PendingCount);
                    handled = true;
                    break;
                case Phase.Dropping:
                    // Already in the save the peer is about to load; discard.
                    handled = true;
                    break;
                default:
                    // Open: this peer has already caught up and is a normal live player now, so just
                    // send the packet. (We only land here for a split second, right after the catch-up
                    // finishes and before its channel is tidied away.)
                    handled = false;
                    break;
            }
        }

        return handled;
    }

    public void ExecuteWorldSend(IPacket packet, Action send)
    {
        if (send == null) throw new ArgumentNullException(nameof(send));

        lock (snapshotGate)
        {
            if (cacheNeutralSendDepth == 0 && !ShouldBypassLoadingQueue(packet))
                joinSnapshot = null;
            send();
        }
    }

    public void ExecuteCacheNeutralWorldSend(Action send)
    {
        if (send == null) throw new ArgumentNullException(nameof(send));

        lock (snapshotGate)
        {
            cacheNeutralSendDepth++;
            try
            {
                send();
            }
            finally
            {
                cacheNeutralSendDepth--;
            }
        }
    }

    public bool TryUseCachedJoinSnapshot(NetPeer peer, out CachedJoinSnapshot snapshot)
    {
        lock (snapshotGate)
        {
            var cached = joinSnapshot;
            if (cached == null ||
                cached.CompressedData == null ||
                DateTime.UtcNow - cached.CutUtc > JoinSnapshotLifetime)
            {
                joinSnapshot = null;
                snapshot = default;
                return false;
            }

            BeginQueueing(peer);
            snapshot = new CachedJoinSnapshot(
                cached.Metadata,
                cached.CompressedData,
                cached.UncompressedSize);
            return true;
        }
    }

    public long CaptureFreshJoinSnapshot(
        NetPeer peer,
        Func<GameSaveDataPacket> capture,
        out GameSaveDataPacket snapshot)
    {
        if (capture == null) throw new ArgumentNullException(nameof(capture));

        lock (snapshotGate)
        {
            snapshot = capture();
            long generation = ++joinSnapshotGeneration;
            joinSnapshot = new JoinSnapshotCacheEntry
            {
                Generation = generation,
                CutUtc = DateTime.UtcNow,
            };
            BeginQueueing(peer);
            return generation;
        }
    }

    public void CompleteJoinSnapshot(
        long generation,
        GameSaveDataPacket snapshot,
        byte[] compressedData)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));

        lock (snapshotGate)
        {
            if (joinSnapshot == null ||
                joinSnapshot.Generation != generation ||
                DateTime.UtcNow - joinSnapshot.CutUtc > JoinSnapshotLifetime)
            {
                return;
            }

            joinSnapshot.Metadata = WithoutRawSave(snapshot);
            joinSnapshot.CompressedData = compressedData;
            joinSnapshot.UncompressedSize = snapshot.GameSaveData?.Length ?? 0;
        }
    }

    public void InvalidateJoinSnapshot()
    {
        lock (snapshotGate)
        {
            joinSnapshot = null;
        }
    }

    private static GameSaveDataPacket WithoutRawSave(GameSaveDataPacket snapshot) =>
        new GameSaveDataPacket(
            null,
            snapshot.CampaignID,
            snapshot.CraftingPlayerData,
            snapshot.WorkshopPlayerData,
            snapshot.CaravansPlayerData,
            snapshot.AlleyPlayerData,
            snapshot.InteractionsPlayerData,
            snapshot.TradePlayerData,
            snapshot.InventoryPlayerData,
            snapshot.AttachmentIdMap,
            snapshot.ServerOptions);

    private static bool ShouldBypassLoadingQueue(IPacket packet)
    {
        // Campaign time is a periodic current-state sample, not history to replay after loading.
        if (packet.PacketType == PacketType.CampaignTime) return true;

        // Lobby membership is connection metadata, not campaign state contained in the save.
        return packet is MessagePacket messagePacket &&
               messagePacket.MessageType == typeof(NetworkSessionLobbyChanged);
    }

    private static bool IsPartyBehaviorSnapshot(IPacket packet) =>
        packet is MessagePacket messagePacket &&
        messagePacket.MessageType == typeof(NetworkUpdatePartyBehavior);

    public void BeginQueueing(NetPeer peer)
    {
        // GetOrAdd guards the (not expected) case where BeginQueueing runs before PlayerConnected was
        // handled: the peer still ends up Queueing rather than silently receiving live broadcasts.
        var channel = channels.GetOrAdd(peer, _ => new PeerChannel());

        lock (channel.Gate)
        {
            channel.Phase = Phase.Queueing;
            channel.PartyBehaviorCoveredByFinalBaseline = true;
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

    public void EndPartyBehaviorBaselineCoverage(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false) return;

        lock (channel.Gate)
        {
            if (channel.Phase == Phase.Queueing)
                channel.PartyBehaviorCoveredByFinalBaseline = false;
        }
    }

    public bool TryGetCatchUpPacketsRemaining(NetPeer peer, out int packetsRemaining)
    {
        packetsRemaining = 0;
        if (channels.TryGetValue(peer, out var channel) == false) return false;

        if (channel.Phase == Phase.Dropping) return false;

        packetsRemaining = Volatile.Read(ref channel.PendingCount) +
                           peer.GetPacketsCountInReliableQueue(0, true) +
                           peer.GetPacketsCountInReliableQueue(0, false);
        return true;
    }

    public string DescribeCatchUp(NetPeer peer)
    {
        if (channels.TryGetValue(peer, out var channel) == false)
        {
            return $"gate=none reliable={ReliableQueueDescription(peer)}";
        }

        lock (channel.Gate)
        {
            return $"gate={channel.Phase} pending={channel.PendingCount} " +
                   $"reliable={ReliableQueueDescription(peer)} enqueued={channel.EnqueuedTotal} " +
                   $"coveredBehavior={channel.CoveredPartyBehaviorTotal} " +
                   $"drained={channel.DrainedTotal} drains={channel.DrainCount} " +
                   $"pendingTypes={DescribeTypes(channel.PendingByType)} " +
                   $"lastDrain={channel.LastDrain}";
        }
    }

    public void OpenWithTail(NetPeer peer, IMessage tailMarker)
    {
        if (tailMarker == null) throw new ArgumentNullException(nameof(tailMarker));

        if (channels.TryGetValue(peer, out var channel) == false)
        {
            network.Value.SendImmediate(peer, tailMarker);
            return;
        }

        int replayed;
        lock (channel.Gate)
        {
            replayed = channel.Pending.Count;
            Drain(peer, channel, "open");

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
            channels.TryRemove(peer, out _);
        }
    }

    private void Drain(NetPeer peer, PeerChannel channel, string reason = "flush")
    {
        var drainedByType = new Dictionary<string, int>();
        int drained = 0;
        while (channel.Pending.Count > 0)
        {
            var packet = channel.Pending.Dequeue();
            string packetType = GetPacketTypeName(packet);
            Decrement(channel.PendingByType, packetType);
            Increment(drainedByType, packetType);
            drained++;
            Interlocked.Decrement(ref channel.PendingCount);
            network.Value.SendImmediate(peer, packet);
        }

        channel.DrainedTotal += drained;
        channel.DrainCount++;
        channel.LastDrain = $"{reason}:{drained}[{DescribeTypes(drainedByType)}]";
    }

    private static string ReliableQueueDescription(NetPeer peer) =>
        $"{peer.GetPacketsCountInReliableQueue(0, true)}/" +
        $"{peer.GetPacketsCountInReliableQueue(0, false)}";

    private static string GetPacketTypeName(IPacket packet)
    {
        if (packet is MessagePacket messagePacket)
            return messagePacket.MessageType?.Name ?? nameof(MessagePacket);

        return packet?.GetType().Name ?? "null";
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static void Decrement(Dictionary<string, int> counts, string key)
    {
        if (!counts.TryGetValue(key, out int count)) return;
        if (count <= 1) counts.Remove(key);
        else counts[key] = count - 1;
    }

    private static string DescribeTypes(Dictionary<string, int> counts)
    {
        if (counts.Count == 0) return "none";
        return string.Join(
            ",",
            counts.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Take(6)
                .Select(pair => $"{pair.Key}:{pair.Value}"));
    }
}
