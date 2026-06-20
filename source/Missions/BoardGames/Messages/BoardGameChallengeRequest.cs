using Common.Messaging;
using ProtoBuf;

namespace Missions.BoardGames.Messages
{
    [ProtoContract]
    public readonly struct BoardGameChallengeRequest : ICommand
    {
        public BoardGameChallengeRequest(string requestingPlayer, string targetPlayer)
        {
            RequestingPlayer = requestingPlayer;
            TargetPlayer = targetPlayer;
        }

        [ProtoMember(1)]
        public string RequestingPlayer { get; }
        [ProtoMember(2)]
        public string TargetPlayer { get; }
    }
}
