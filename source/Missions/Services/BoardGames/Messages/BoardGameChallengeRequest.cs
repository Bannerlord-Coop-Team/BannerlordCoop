using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Services.BoardGames.Messages
{
    [ProtoContract]
    public readonly struct BoardGameChallengeRequest : INetworkEvent
    {
        public BoardGameChallengeRequest(Guid requestingPlayer, Guid targetPlayer)
        {
            RequestingPlayer = requestingPlayer;
            TargetPlayer = targetPlayer;
        }

        [ProtoMember(1)]
        public Guid RequestingPlayer { get; }
        [ProtoMember(2)]
        public Guid TargetPlayer { get; }
    }
}
