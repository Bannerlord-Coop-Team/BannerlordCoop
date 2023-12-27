using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Common.Network;

/// <inheritdoc cref="INetwork"/>
public abstract class CoopNetworkBase : INetwork
{
    public INetworkConfiguration Configuration { get; }
    public abstract int Priority { get; }

    protected CoopNetworkBase(INetworkConfiguration configuration)
    {
        Configuration = configuration;
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
        byte[] data = ProtoBufSerializer.Serialize(packet);

        // Send data
        netPeer.Send(data, packet.DeliveryMethod);
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        var eventPacket = new EventPacket(message);
        Send(netPeer, eventPacket);
    }

    public void SendAll(IMessage message)
    {
        var eventPacket = new EventPacket(message);
        SendAll(eventPacket);
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        var eventPacket = new EventPacket(message);
        SendAllBut(excludedPeer, eventPacket);
    }

    public abstract void Start();
    public abstract void Stop();
    public abstract void SendAll(IPacket packet);
    public abstract void SendAllBut(NetPeer ignoredPeer, IPacket packet);
    public abstract void Update(TimeSpan frameTime);
}
