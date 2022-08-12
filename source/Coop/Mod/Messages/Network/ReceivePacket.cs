using LiteNetLib;

namespace Coop.Mod.Messages.Network
{
    public readonly struct ReceivePacket
    {
        public ReceivePacket(NetPeer peer, NetPacketReader writer, DeliveryMethod deliveryMethod)
        {
            Peer = peer;
            Writer = writer;
            DeliveryMethod = deliveryMethod;
        }

        public NetPeer Peer { get; }
        public NetPacketReader Writer { get; }
        public DeliveryMethod DeliveryMethod { get; }
    }
}
