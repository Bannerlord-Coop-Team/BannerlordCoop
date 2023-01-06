using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages.BoardGames
{
    [ProtoContract]
    public readonly struct ForfeitGameMessage : INetworkEvent
    {
        public ForfeitGameMessage(Guid gameId)
        {
            GameId = gameId;
        }

        [ProtoMember(1)]
        public Guid GameId { get; }
    }
}
