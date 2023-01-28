using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Outgoing
{
    public readonly struct PlayerRecievingSave : IEvent
    {
        public NetPeer PlayerId { get; }

        public PlayerRecievingSave(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}