using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct PeerDisconnected : IEvent
    {
        public NetPeer NetPeer { get; }
        public DisconnectInfo DisconnectInfo { get; }

        public PeerDisconnected(NetPeer netPeer, DisconnectInfo disconnectInfo)
        {
            NetPeer = netPeer;
            DisconnectInfo = disconnectInfo;
        }
    }
}
