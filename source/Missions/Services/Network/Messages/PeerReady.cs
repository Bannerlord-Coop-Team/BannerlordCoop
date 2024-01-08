using Common.Messaging;
using LiteNetLib;

namespace Missions.Services.Network.Messages
{
    /// <summary>
    /// Event for when a peer events are able to be processed
    /// </summary>
    public readonly struct PeerReady : IEvent
    {
        public NetPeer Peer { get; }

        public PeerReady(NetPeer peer)
        {
            Peer = peer;
        }
    }
}
