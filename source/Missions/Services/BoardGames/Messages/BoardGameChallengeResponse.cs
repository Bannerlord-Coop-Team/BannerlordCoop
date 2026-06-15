using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Services.BoardGames.Messages
{
    [ProtoContract]
    public readonly struct BoardGameChallengeResponse : IEvent
    {
        public BoardGameChallengeResponse(Guid requestingPlayer, Guid targetPlayer, bool accepted, Guid gameId)
        {
            RequestingPlayer = requestingPlayer;
            TargetPlayer = targetPlayer;
            Accepted = accepted;
            GameId = gameId;
        }
        [ProtoMember(1)]
        public Guid RequestingPlayer { get; }
        [ProtoMember(2)]
        public Guid TargetPlayer { get; }
        [ProtoMember(3)]
        public bool Accepted { get; }
        [ProtoMember(4)]
        public Guid GameId { get; }
    }
}
