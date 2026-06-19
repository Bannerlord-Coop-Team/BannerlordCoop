using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Missions.BoardGames.Messages
{
    [ProtoContract]
    public readonly struct ForfeitGameMessage : ICommand
    {
        public ForfeitGameMessage(Guid gameId)
        {
            GameId = gameId;
        }

        [ProtoMember(1)]
        public Guid GameId { get; }
    }
}
