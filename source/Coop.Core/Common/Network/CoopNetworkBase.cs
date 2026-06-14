using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
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
    public INetworkConfig Configuration { get; }
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
        Configuration = configuration;
        this.serializer = serializer;

        netManager = new NetManager(this)
        {
            DisconnectTimeout = (int)configuration.ConnectionTimeout.TotalMilliseconds
        };

        CancellationTokenSource = new CancellationTokenSource();
        poller = new Poller(Update, Configuration.NetworkPollInterval);
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
    /// Sends straight to the peer, bypassing any per-peer send gating (the server's connection queue).
    /// For connection-level traffic that must reach a peer regardless of its load state — the transfer
    /// save and the join handshake — and for the queue's own replay.
    /// </summary>
    public void SendImmediate(NetPeer netPeer, IPacket packet)
    {
        SendInternal(netPeer, packet);
    }

    public void SendImmediate(NetPeer netPeer, IMessage message)
    {
        SendInternal(netPeer, MessagePacket.Create(message, serializer));
    }

    private void SendInternal(NetPeer netPeer, IPacket packet)
    {
        // Serialize data
        byte[] data = serializer.Serialize(packet);

        // Profile every outbound packet (server only). MessagePacket carries its message type, so the
        // profile breaks message traffic out by message type rather than one opaque MessagePacket bucket.
        packetProfiler.Record(packet, data.Length);

        // Send data
        netPeer.Send(data, packet.DeliveryMethod);
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