using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages.BoardGames
{
    [ProtoContract]
    public readonly struct PawnCapturedMessage : INetworkEvent
    {
        public PawnCapturedMessage(Guid gameId, int index)
        {
            GameId = gameId;
            Index = index;
        }

        [ProtoMember(1)]
        public Guid GameId { get; }
        [ProtoMember(2)]
        public int Index { get; }
    }
}
