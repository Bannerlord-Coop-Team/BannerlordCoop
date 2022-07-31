using LiteNetLib;

namespace Coop.NetImpl.LiteNet
{
    public enum PacketType : byte
    {
        Movement,
        Event,
    }
    public interface IPacketHandler
    {
        /// <summary>
        /// Type of packet to handle
        /// </summary>
        PacketType PacketType { get; }

        /// <summary>
        /// Handler for specified packet type
        /// </summary>
        /// <param name="peer">Peer that sent the packet</param>
        /// <param name="packet">Packet of packet type</param>
        void HandlePacket(NetPeer peer, IPacket packet);

        /// <summary>
        /// Handler for when a peer disconnects from this client.
        /// </summary>
        /// <param name="peer">Peer that disconnected</param>
        /// <param name="reason">Reason for disconnect</param>
        void HandlePeerDisconnect(NetPeer peer, DisconnectInfo reason);
    }
}