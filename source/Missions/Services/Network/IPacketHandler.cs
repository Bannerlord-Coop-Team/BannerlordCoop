using LiteNetLib;
using Missions.Services.Network;

namespace Missions.Services.Network
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
    }
}
