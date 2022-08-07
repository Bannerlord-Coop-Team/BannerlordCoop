using System;
using Common.Components;
using LiteNetLib;

namespace Coop.Communication.PacketHandlers
{
    public interface IPacketManager : IComponent, IDisposable
    {
        void Initialize(NetManager netManager);

        void Handle(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod);

        bool RegisterPacketHandler(IPacketHandler handler);

        bool RemovePacketHandler(IPacketHandler handler);

        /// <summary>
        /// Sends packet to all clients except for the given client.
        /// </summary>
        /// <remarks>
        /// This method is meant to be used for forwarding packets from the server.
        /// </remarks>
        /// <param name="netPeer">Peer to omit sending.</param>
        /// <param name="packet">Packet to send.</param>
        void SendAllBut(NetPeer netPeer, IPacket packet);

        /// <summary>
        /// Sends packet to all connected clients.
        /// </summary>
        /// <param name="packet">Packet to send.</param>
        void SendAll(IPacket packet);

        /// <summary>
        /// Sends a packet to the given client.
        /// </summary>
        /// <param name="netPeer">Client to send packet to.</param>
        /// <param name="packet">Packet to send.</param>
        void Send(NetPeer netPeer, IPacket packet);
    }
}