using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;
using System;

namespace Common.Network;

/// <summary>
/// Manages basic network functionality
/// </summary>
public interface INetwork : IDisposable
{
    INetworkConfiguration Configuration { get; }

    void Send(NetPeer netPeer, IPacket packet);
    void SendAll(IPacket packet);
    void SendAllBut(NetPeer excludedPeer, IPacket packet);
    void Send(NetPeer netPeer, IMessage message);
    void SendAll(IMessage message);
    void SendAllBut(NetPeer excludedPeer, IMessage message);
    void Start();
}
