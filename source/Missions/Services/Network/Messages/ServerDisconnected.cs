using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct ServerDisconnected : IEvent
    {
        public NetPeer NetPeer { get; }
        public DisconnectInfo DisconnectInfo { get; }

        public ServerDisconnected(NetPeer netPeer, DisconnectInfo disconnectInfo)
        {
            NetPeer = netPeer;
            DisconnectInfo = disconnectInfo;
        }
    }
}
