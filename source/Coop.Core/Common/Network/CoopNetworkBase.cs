using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using LiteNetLib;
using ProtoBuf.Meta;
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
    public INetworkConfiguration Configuration { get; }
    public abstract int Priority { get; }

    protected readonly ICommonSerializer serializer;

    private Thread UpdateThread { get; set; }
    private CancellationTokenSource CancellationTokenSource;
    private bool Disposed;

    protected readonly NetManager netManager;

    protected CoopNetworkBase(INetworkConfiguration configuration, ICommonSerializer serializer)
    {
        Configuration = configuration;
        this.serializer = serializer;

        netManager = new NetManager(this);
        netManager.UnconnectedMessagesEnabled = true;
        netManager.IPv6Enabled = false;

        // netManager.DisconnectTimeout = configuration.ConnectionTimeout.Milliseconds;

        // Increase disconnect timeout to prevent disconnect during debugging
        netManager.DisconnectTimeout = 300 * 1000;

        CancellationTokenSource = new CancellationTokenSource();
        UpdateThread = new Thread(UpdateThreadMethod);
        UpdateThread.Start();
    }

    ~CoopNetworkBase()
    {
        Dispose();
    }

    public virtual void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        try { netManager.Stop(); } catch { }

        try { CancellationTokenSource?.Cancel(); } catch (ObjectDisposedException) { }
        try { CancellationTokenSource?.Dispose(); } catch { }
        CancellationTokenSource = null;

        try { UpdateThread?.Join(Configuration.ObjectCreationTimeout); } catch { }
    }

    private void UpdateThreadMethod()
    {
        var lastTime = DateTime.Now;
        while (true)
        {
            var cts = CancellationTokenSource;
            if (cts == null || cts.IsCancellationRequested) break;

            var now = DateTime.Now;
            var deltaTime = now - lastTime;
            lastTime = now;
            Update(deltaTime);

            var poll = Configuration != null ? Configuration.NetworkPollInterval : TimeSpan.FromMilliseconds(50);
            Thread.Sleep(poll);
        }
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
        // Serialize data
        byte[] data = serializer.Serialize(packet);

        // Send data
        netPeer.Send(data, packet.DeliveryMethod);
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        var data = SerializeMessage(message);
        var eventPacket = new MessagePacket(data);
        Send(netPeer, eventPacket);
    }

    public void SendAll(IMessage message)
    {
        var data = SerializeMessage(message);
        var eventPacket = new MessagePacket(data);
        SendAll(eventPacket);
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        var data = SerializeMessage(message);
        var eventPacket = new MessagePacket(data);
        SendAllBut(excludedPeer, eventPacket);
    }

    private byte[] SerializeMessage(IMessage message)
    {
        if (RuntimeTypeModel.Default.IsDefined(message.GetType()) == false)
        {
            throw new ArgumentException($"Type {message.GetType().Name} is not serializable.");
        }

        return serializer.Serialize(message);
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
