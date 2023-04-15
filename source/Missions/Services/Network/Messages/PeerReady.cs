using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct PeerReady : IEvent
    {
        public NetPeer Peer { get; }

        public PeerReady(NetPeer peer)
        {
            Peer = peer;
        }
    }
}
