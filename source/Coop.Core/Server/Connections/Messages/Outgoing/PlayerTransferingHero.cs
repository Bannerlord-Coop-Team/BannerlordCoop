using Common.Messaging;
using GameInterface.Serialization.External;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Connections.Messages.Outgoing
{
    public readonly struct PlayerTransferingHero : IEvent
    {
        public NetPeer PlayerId { get; }

        public PlayerTransferingHero(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
