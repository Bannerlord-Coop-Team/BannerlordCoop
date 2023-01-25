using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerCreatingCharacter : INetworkEvent
    {
        public PlayerCreatingCharacter(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
