using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Outgoing
{
    public readonly struct PlayerCreatingCharacter : IEvent
    {
        public NetPeer PlayerId { get; }

        public PlayerCreatingCharacter(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
