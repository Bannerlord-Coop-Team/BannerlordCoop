using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record KingdomDecisionVoteRequested : IEvent
    {
        public KingdomDecisionVoteData VoteData { get; }

        public KingdomDecisionVoteRequested(KingdomDecisionVoteData voteData)
        {
            VoteData = voteData;
        }
    }
}
