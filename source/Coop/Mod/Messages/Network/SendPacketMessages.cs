using Coop.Communication.PacketHandlers;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Messages.Network
{
    /// <summary>
    /// Broadcasts packet to all connected clients
    /// </summary>
    public readonly struct BroadcastPacket
    {
        public BroadcastPacket(IPacket packet)
        {
            Packet = packet;
        }
        public IPacket Packet { get; }
    }

    /// <summary>
    /// Forwards message from client to
    /// </summary>
    public readonly struct ForwardPacket
    {
        public ForwardPacket(NetPeer sendingClient, IPacket packet)
        {
            SendingClient = sendingClient;
            Packet = packet;
        }
        public NetPeer SendingClient { get; }
        public IPacket Packet { get; }
    }

    public readonly struct SendPacket
    {
        public SendPacket(NetPeer clientToSend, IPacket packet)
        {
            ClientToSend = clientToSend;
            Packet = packet;
        }
        public NetPeer ClientToSend { get; }
        public IPacket Packet { get; }
    }
}
