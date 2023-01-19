

using Common;
using Coop.Core.Communication.PacketHandlers;
using Coop.Core.Configuration;
using LiteNetLib;

namespace Coop.Core
{
    public interface ICoopNetwork : IUpdateable
    {
        INetworkConfiguration Configuration { get; }

        void Send(NetPeer netPeer, IPacket packet);
        void SendAll(IPacket packet);
        void SendAllBut(NetPeer netPeer, IPacket packet);
        void Start();
        void Stop();
    }
}
