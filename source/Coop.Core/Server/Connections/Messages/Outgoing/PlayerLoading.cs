using Common.Messaging;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages.Outgoing
{
    public readonly struct PlayerLoading : IEvent
    {
        public NetPeer PlayerId { get; }

        public PlayerLoading(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
