using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkRequestKingdomDecisionVote : ICommand
    {
        [ProtoMember(1)]
        public KingdomDecisionVoteData VoteData { get; }

        public NetworkRequestKingdomDecisionVote(KingdomDecisionVoteData voteData)
        {
            VoteData = voteData;
        }
    }
}
