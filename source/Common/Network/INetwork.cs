using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;
using System;
using System.Net;

namespace Common.Network;

/// <summary>
/// Manages basic network functionality
/// </summary>
public interface INetwork : IDisposable
{
    INetworkConfig Config { get; }

    void Send(NetPeer netPeer, IPacket packet);
    void SendImmediate(NetPeer netPeer, IPacket packet);
    void SendAll(IPacket packet);
    void SendAllBut(NetPeer excludedPeer, IPacket packet);
    void Send(NetPeer netPeer, IMessage message);
    void SendImmediate(NetPeer netPeer, IMessage message);
    void SendAll(IMessage message);
    void SendAllBut(NetPeer excludedPeer, IMessage message);
    void Start();
}
