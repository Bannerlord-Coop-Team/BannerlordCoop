using LiteNetLib;

namespace Coop.Mod
{
    public interface IPacketManager
    {
        void Handle(NetPeer peer, NetPacketReader writer, DeliveryMethod deliveryMethod);
    }
}