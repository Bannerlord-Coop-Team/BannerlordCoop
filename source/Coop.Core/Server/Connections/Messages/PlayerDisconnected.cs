using Common.Messaging;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerDisconnected : IEvent
    {
        public PlayerDisconnected(NetPeer playerId, DisconnectInfo disconnectInfo)
        {
            PlayerId = playerId;
            DisconnectInfo = disconnectInfo;
        }

        public NetPeer PlayerId { get; }
        public DisconnectInfo DisconnectInfo { get; }
    }
}
