using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    public readonly struct PlayerConnected : IEvent
    {
        public NetPeer PlayerId { get; }

        public PlayerConnected(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
