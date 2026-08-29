using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using Coop.Core.Common.Network.Packets;
using LiteNetLib;
using System;
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
    private readonly CancellationTokenSource sessionCancellation;
    private int pollerStarted;

    // Profiles outbound packets; dumps per-type counts and byte totals every 10 seconds (server only).
    private readonly PacketProfiler packetProfiler = new PacketProfiler(TimeSpan.FromSeconds(10));
    private readonly IReliableMessageBatcher<NetPeer> reliableMessageBatcher;

    // Guard against repeated container and explicit disposal.
    private int disposed;

    protected readonly NetManager netManager;

    protected CoopNetworkBase(
        INetworkConfig configuration,
        ICommonSerializer serializer,
        IReliableMessageBatcher<NetPeer> reliableMessageBatcher,
        CancellationTokenSource sessionCancellation)
    {
        if (reliableMessageBatcher == null)
            throw new ArgumentNullException(nameof(reliableMessageBatcher));

        Config = configuration;
        this.serializer = serializer;
        this.sessionCancellation = sessionCancellation;
        this.reliableMessageBatcher = reliableMessageBatcher;
        this.reliableMessageBatcher.AggregateSent += RecordAggregateSent;

        netManager = new NetManager(this)
        {
            DisconnectTimeout = (int)configuration.DisconnectTimeout.TotalMilliseconds,
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

        poller = new Poller(UpdateWithinSession, Config.NetworkPollInterval);
    }

    private void UpdateWithinSession(TimeSpan frameTime)
    {
        using (GameThread.ActivateCancellation(sessionCancellation.Token))
        {
            if (sessionCancellation.IsCancellationRequested) return;

            try
            {
                Update(frameTime);
            }
            catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
            {
                // Teardown woke a blocking game-thread marshal in this callback.
            }
        }
    }

    protected void StartNetworkPoller()
    {
        if (Interlocked.CompareExchange(ref pollerStarted, 1, 0) == 0)
        {
            poller.Start();
        }
    }

    public virtual void Dispose()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        if (poller.IsPollingThread)
        {
            throw new InvalidOperationException(
                "The network must be disposed after its poll callback returns.");
        }
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        // Wake blocking game-thread marshals before waiting for the poll callback to return.
        sessionCancellation.Cancel();
        poller.StopAndWait(Timeout.InfiniteTimeSpan);
        reliableMessageBatcher.AggregateSent -= RecordAggregateSent;
        packetProfiler.Dispose();
        reliableMessageBatcher.Clear();
        netManager.Stop();
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
    /// Sends a packet straight to the peer, bypassing any per-peer send gating. Queued replay
    /// <see cref="MessagePacket"/>s retain normal aggregation; non-message packets such as the
    /// transfer save are sent directly.
    /// </summary>
    public void SendImmediate(NetPeer netPeer, IPacket packet)
    {
        SendInternal(netPeer, packet);
    }

    /// <summary>
    /// Sends connection-level message traffic straight to the peer and flushes any earlier aggregated
    /// replay first, so handshake messages remain exact reliable-ordered barriers.
    /// </summary>
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
            var worldQueued = peer.GetPacketsCountInReliableQueue(0, true) +
                              peer.GetPacketsCountInReliableQueue(0, false);
            var bulkQueued = peer.GetPacketsCountInReliableQueue(BulkChannel, true) +
                             peer.GetPacketsCountInReliableQueue(BulkChannel, false);
            return $"{peer.Id}@{peer.Address} worldQueue={worldQueued} bulkQueue={bulkQueued} ping={peer.Ping}ms";
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

            if (immediate)
                reliableMessageBatcher.SendImmediate(netPeer, payload, SendReliableMessagePayload);
            else
                reliableMessageBatcher.Send(netPeer, payload, SendReliableMessagePayload);
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
            reliableMessageBatcher.FlushThen(
                netPeer,
                SendReliableMessagePayload,
                () => netPeer.Send(data, GetChannel(packet), packet.DeliveryMethod));
            return;
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

    private static byte GetChannel(IPacket packet) => packet is GameSaveDataPacket or GameSaveDataChunkPacket ? BulkChannel : (byte)0;

    /// <summary>
    /// Sends every peer's buffered messages and prunes buffers of disconnected peers. Normally called
    /// from the network update; the join-save path also calls it while the poll thread is blocked.
    /// </summary>
    public void FlushPendingMessages()
    {
        reliableMessageBatcher.FlushAll(
            peer => peer.ConnectionState == ConnectionState.Connected,
            SendReliableMessagePayload);
    }

    private void RecordAggregateSent(AggregateMessagePacket packet, int framingOverhead)
    {
        packetProfiler.Record(packet, framingOverhead);
    }

    private static void SendReliableMessagePayload(NetPeer netPeer, byte[] payload)
    {
        netPeer.Send(payload, DeliveryMethod.ReliableOrdered);
    }

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
