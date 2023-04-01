using Common.Messaging;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Connections.Messages
{
    [ProtoContract]
    public readonly struct NetworkClientValidate : INetworkEvent
    {
        [ProtoMember(1)]
        public string PlayerId { get; }

        public NetworkClientValidate(string playerId)
        {
            PlayerId = playerId;
        }
    }

    [ProtoContract]
    public readonly struct NetworkClientValidated : INetworkEvent
    {
        [ProtoMember(1)]
        public bool HeroExists { get; }
        [ProtoMember(2)]
        public Guid HeroId { get; }

        public NetworkClientValidated(bool heroExists, Guid heroId)
        {
            HeroExists = heroExists;
            HeroId = heroId;
        }
    }
}
