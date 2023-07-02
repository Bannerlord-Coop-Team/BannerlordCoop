using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct PeerConnected : IEvent
    {
        public NetPeer Peer { get; }

        public PeerConnected(NetPeer peer)
        {
            Peer = peer;
        }
    }
}
