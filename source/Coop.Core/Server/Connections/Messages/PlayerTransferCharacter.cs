using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerTransferCharacter
    {
        public PlayerTransferCharacter(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
