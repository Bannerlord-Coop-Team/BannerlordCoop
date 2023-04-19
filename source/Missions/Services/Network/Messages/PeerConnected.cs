using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    public readonly struct PeerConnected
    {
        public NetPeer Peer { get; }

        public PeerConnected(NetPeer peer)
        {
            Peer = peer;
        }
    }
}
