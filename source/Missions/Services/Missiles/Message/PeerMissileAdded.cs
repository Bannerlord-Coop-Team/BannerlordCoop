using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Missiles.Message
{
    /// <summary>
    /// Missile added event originating from a network event
    /// </summary>
    public readonly struct PeerMissileAdded : IEvent
    {
        public NetPeer Peer { get; }
        public int PeerMissileId { get; }
        public int LocalMissileId { get; }

        public PeerMissileAdded(NetPeer peer, int peerMissileId, int localMissileId)
        {
            Peer = peer;
            PeerMissileId = peerMissileId;
            LocalMissileId = localMissileId;
        }
    }
}