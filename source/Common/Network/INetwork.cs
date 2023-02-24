using Common.PacketHandlers;
using LiteNetLib;

namespace Common.Network
{
    public interface INetwork : IUpdateable
    {
        INetworkConfiguration Configuration { get; }

        void Send(NetPeer netPeer, IPacket packet);
        void SendAll(IPacket packet);
        void SendAllBut(NetPeer netPeer, IPacket packet);
        void Start();
        void Stop();
    }
}
