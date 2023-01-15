﻿using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerConnected : IEvent
    {
        public PlayerConnected(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
