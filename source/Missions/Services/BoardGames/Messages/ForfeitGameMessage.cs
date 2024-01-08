using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Services.BoardGames.Messages
{
    [ProtoContract]
    public readonly struct ForfeitGameMessage : IEvent
    {
        public ForfeitGameMessage(Guid gameId)
        {
            GameId = gameId;
        }

        [ProtoMember(1)]
        public Guid GameId { get; }
    }
}
