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

    protected readonly NetManager netManager;

    protected CoopNetworkBase(INetworkConfiguration configuration, ICommonSerializer serializer)
    {
        Configuration = configuration;
        this.serializer = serializer;

        netManager = new NetManager(this);

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
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        UpdateThread?.Join(Configuration.ObjectCreationTimeout);

        netManager.DisconnectAll();
        netManager.Stop();
    }

    private void UpdateThreadMethod()
    {
        var lastTime = DateTime.Now;
        while (CancellationTokenSource.IsCancellationRequested == false)
        {
            var now = DateTime.Now;
            TimeSpan deltaTime = now - lastTime;
            lastTime = now;
            Update(deltaTime);
            Thread.Sleep(Configuration.NetworkPollInterval);
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
