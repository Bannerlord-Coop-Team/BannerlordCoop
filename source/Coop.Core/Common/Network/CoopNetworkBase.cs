using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using Coop.Core.Common.Network.Packets;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Coop.Core.Common.Network;

/// <inheritdoc cref="INetwork"/>
public abstract class CoopNetworkBase : INetwork, INetEventListener
{
    public INetworkConfig Config { get; }
    public abstract int Priority { get; }

    protected readonly ICommonSerializer serializer;

    private readonly Poller poller;

    // Profiles outbound packets; dumps per-type counts and byte totals every 10 seconds (server only).
    private readonly PacketProfiler packetProfiler = new PacketProfiler(TimeSpan.FromSeconds(10));

    private CancellationTokenSource CancellationTokenSource;
    // Guard against double-dispose: finalizer calls Dispose() after explicit Dispose() on reconnect
    private bool _disposed = false;

    protected readonly NetManager netManager;

    protected CoopNetworkBase(INetworkConfig configuration, ICommonSerializer serializer)
    {
        Config = configuration;
        this.serializer = serializer;

        netManager = new NetManager(this)
        {
            DisconnectTimeout = (int)configuration.ConnectionTimeout.TotalMilliseconds,
            // Two reliable lanes: 0 for the world-change stream, BulkChannel for large transfers.
            // Each channel has its own (small, fixed) reliable window, so a multi-MB transfer
            // saturating its own lane cannot head-of-line block world sync or be counted against
            // the channel-0 queue depth that triggers the catch-up pause.
            ChannelsCount = 2,
            // LiteNetLib's internal logic-thread cycle: resends, merges and window advances happen at
            // this cadence, so it directly bounds how fast a backed-up reliable channel drains.
            UpdateTime = (int)configuration.UpdateTime.TotalMilliseconds
        };

        // Reliable-queue depth per peer is what OverloadedPeerManager pauses campaign time on, so
        // surface it (plus ping) in every profile dump to make congestion visible in the log.
        packetProfiler.ExtraStatsProvider = DescribePeerQueues;

        CancellationTokenSource = new CancellationTokenSource();
        poller = new Poller(Update, Config.NetworkPollInterval);
        poller.Start();
    }

    ~CoopNetworkBase()
    {
        Dispose();
    }

    public virtual void Dispose()
    {
        // Prevent ObjectDisposedException if GC finalizer runs after explicit Dispose on reconnect
        if (_disposed) return;
        _disposed = true;

        netManager.Stop();

        packetProfiler.Dispose();

        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();

        // Tell GC not to run the finalizer — Dispose() already cleaned up, avoids double-call
        GC.SuppressFinalize(this);
    }

    public virtual void SendAllBut(NetManager netManager, NetPeer netPeer, IPacket packet)
    {
        var peers = new List<NetPeer>();
        netManager.GetPeersNonAlloc(peers, ConnectionState.Connected);
        foreach (NetPeer peer in peers.Where(peer => peer != netPeer))
        {
            Send(peer, packet);
        }
    }

    protected virtual void SendAll(NetManager netManager, IPacket packet)
    {
        var peers = new List<NetPeer>();
        netManager.GetPeersNonAlloc(peers, ConnectionState.Connected);
        foreach (var peer in peers)
        {
            Send(peer, packet);
        }
    }

    public virtual void Send(NetPeer netPeer, IPacket packet)
    {
        SendInternal(netPeer, packet);
    }

    /// <summary>
    /// Sends straight to the peer, bypassing any per-peer send gating (the server's connection queue)
    /// and message aggregation. For connection-level traffic that must reach a peer regardless of its
    /// load state — the transfer save and the join handshake — and for the queue's own replay.
    /// </summary>
    public void SendImmediate(NetPeer netPeer, IPacket packet)
    {
        SendInternal(netPeer, packet, immediate: true);
    }

    public void SendImmediate(NetPeer netPeer, IMessage message)
    {
        SendInternal(netPeer, MessagePacket.Create(message, serializer), immediate: true);
    }

    private string DescribePeerQueues()
    {
        var peers = new List<NetPeer>();
        netManager.GetPeersNonAlloc(peers, ConnectionState.Connected);
        if (peers.Count == 0) return null;

        return "peer queues: " + string.Join(", ", peers.Select(peer =>
        {
            var queued = peer.GetPacketsCountInReliableQueue(0, true) +
                         peer.GetPacketsCountInReliableQueue(0, false);
            return $"{peer.Id}@{peer.Address} queue={queued} ping={peer.Ping}ms";
        }));
    }

    private void SendInternal(NetPeer netPeer, IPacket packet)
    {
        SendInternal(netPeer, packet, immediate: false);
    }

    private void SendInternal(NetPeer netPeer, IPacket packet, bool immediate)
    {
        if (packet is MessagePacket messagePacket)
        {
            // A MessagePacket already holds a fully serialized, self-identifying message wrapper in
            // Data; that payload goes on the wire directly (bare, the historical format) or inside an
            // aggregate envelope — never serialized a second time.
            byte[] payload = messagePacket.Data;

            // Profile at the logical send so message traffic stays broken out by message type; the
            // envelope's framing overhead is recorded separately when a batch actually leaves.
            packetProfiler.Record(packet, payload.Length);

            if (immediate || payload.Length >= AggregationBudgetBytes)
            {
                // Keep the reliable stream's order: everything buffered was logically sent first.
                FlushPeerMessages(netPeer);
                netPeer.Send(payload, packet.DeliveryMethod);
                return;
            }

            EnqueueMessage(netPeer, payload);
            return;
        }

        byte[] data = serializer.Serialize(packet);
        packetProfiler.Record(packet, data.Length);

        // Buffered messages were logically sent before this packet; when it rides the reliable
        // stream, flush them first so that stream preserves the order (the transfer save relies on
        // it). Unreliable/sequenced packets travel on other channels with no ordering relationship,
        // so flushing for them would only fragment batches (e.g. 4x/sec for the time heartbeat).
        if (packet.DeliveryMethod == DeliveryMethod.ReliableOrdered ||
            packet.DeliveryMethod == DeliveryMethod.ReliableUnordered)
        {
            FlushPeerMessages(netPeer);
        }

        netPeer.Send(data, GetChannel(packet), packet.DeliveryMethod);
    }

    /// <summary>
    /// Reliable channel reserved for large transfers. The joining peer's save fragments drain here
    /// without occupying the world-sync channel's reliable window, and without inflating the
    /// channel-0 queue depth <c>OverloadedPeerManager</c> pauses campaign time on. Safe for the save
    /// despite leaving the message stream's channel: the server withholds world deltas until the
    /// client has loaded — which requires the full save — so nothing can overtake it observably.
    /// </summary>
    public const byte BulkChannel = 1;

    protected const int MaxRegularInboundPayloadBytes = 20 * 1024 * 1024;
    protected const int BulkEnvelopeOverheadBytes = 4 * 1024 * 1024;

    protected static int GetMaxInboundPayloadBytes(byte channelNumber)
    {
        return channelNumber == BulkChannel
            ? SaveDataCompression.MaxCompressedBytes + BulkEnvelopeOverheadBytes
            : MaxRegularInboundPayloadBytes;
    }

    private static byte GetChannel(IPacket packet) => packet is GameSaveDataPacket ? BulkChannel : (byte)0;

    #region Message aggregation

    /// <summary>
    /// Combined payload budget per aggregate batch. LiteNetLib's reliable channel caps unacked
    /// packets — not bytes — in flight (a hardcoded 64-packet window in 1.3.1), so per-peer
    /// throughput scales with packet fullness.
    /// </summary>
    /// <remarks>
    /// Why 1200: a LiteNetLib connection starts at InitialMtu (1024B) and probes upward ("MTU
    /// discovery") to MaxPacketSize (1432B = Ethernet 1500 minus IP/UDP/LiteNetLib headers),
    /// normally settling at 1432 within seconds. 1200B of payload plus envelope framing (~10-30B
    /// protobuf + 4B channeled header) fits one 1432B datagram — one packet, one window slot. On a
    /// link still at 1024 (pre-discovery, or a path that never upgrades) the envelope splits into
    /// two reliable fragments — two window slots, still ~10x fewer than one packet per message.
    /// </remarks>
    public const int AggregationBudgetBytes = 1200;

    private sealed class PeerMessageBuffer
    {
        public readonly object Lock = new object();
        public readonly MessageAggregationBuffer Buffer = new MessageAggregationBuffer(AggregationBudgetBytes);
    }

    private readonly ConcurrentDictionary<NetPeer, PeerMessageBuffer> pendingMessages =
        new ConcurrentDictionary<NetPeer, PeerMessageBuffer>();

    private void EnqueueMessage(NetPeer netPeer, byte[] payload)
    {
        var peerBuffer = pendingMessages.GetOrAdd(netPeer, _ => new PeerMessageBuffer());
        lock (peerBuffer.Lock)
        {
            var overflow = peerBuffer.Buffer.Append(payload);
            // Sending under the peer's lock keeps batches in enqueue order on the reliable stream.
            if (overflow != null) SendBatch(netPeer, overflow);
        }
    }

    private void FlushPeerMessages(NetPeer netPeer)
    {
        if (pendingMessages.TryGetValue(netPeer, out var peerBuffer) == false) return;

        lock (peerBuffer.Lock)
        {
            var batch = peerBuffer.Buffer.Drain();
            if (batch != null) SendBatch(netPeer, batch);
        }
    }

    /// <summary>
    /// Sends every peer's buffered messages and prunes buffers of disconnected peers. Called from the
    /// subclasses' network <c>Update</c>, so a sub-budget trickle waits at most one poll interval.
    /// </summary>
    protected void FlushPendingMessages()
    {
        foreach (var entry in pendingMessages)
        {
            if (entry.Key.ConnectionState != ConnectionState.Connected)
            {
                pendingMessages.TryRemove(entry.Key, out _);
                continue;
            }

            FlushPeerMessages(entry.Key);
        }
    }

    private void SendBatch(NetPeer netPeer, List<byte[]> payloads)
    {
        // A batch of one goes bare — the historical wire format — sparing the envelope overhead.
        if (payloads.Count == 1)
        {
            netPeer.Send(payloads[0], DeliveryMethod.ReliableOrdered);
            return;
        }

        var envelope = new AggregateMessagePacket(payloads.ToArray());
        byte[] data = serializer.Serialize(envelope);

        // Inner messages were profiled at their logical send; record only the framing overhead here,
        // which also makes the number of wire packets aggregation produced visible in the profile.
        int framingOverhead = data.Length;
        foreach (var payload in payloads) framingOverhead -= payload.Length;
        packetProfiler.Record(envelope, framingOverhead);

        netPeer.Send(data, envelope.DeliveryMethod);
    }

    #endregion

    public void Send(NetPeer netPeer, IMessage message)
    {
        Send(netPeer, MessagePacket.Create(message, serializer));
    }

    public void SendAll(IMessage message)
    {
        SendAll(MessagePacket.Create(message, serializer));
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        SendAllBut(excludedPeer, MessagePacket.Create(message, serializer));
    }

    public abstract void Start();
    public abstract void SendAll(IPacket packet);
    public abstract void SendAllBut(NetPeer ignoredPeer, IPacket packet);
    public abstract void Update(TimeSpan frameTime);

    public abstract void OnPeerConnected(NetPeer peer);

    public abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);

    public abstract void OnNetworkError(IPEndPoint endPoint, SocketError socketError);

    public abstract void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod);

    public abstract void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);

    public abstract void OnNetworkLatencyUpdate(NetPeer peer, int latency);

    public abstract void OnConnectionRequest(ConnectionRequest request);
}
