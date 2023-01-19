using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerCreatingCharacter
    {
        public PlayerCreatingCharacter(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
