using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerLoaded
    {
        public PlayerLoaded(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
