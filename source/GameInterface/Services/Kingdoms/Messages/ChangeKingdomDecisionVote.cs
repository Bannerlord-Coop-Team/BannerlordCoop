using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ChangeKingdomDecisionVote : ICommand
    {
        public string ControllerId { get; }
        public KingdomDecisionVoteData VoteData { get; }

        public ChangeKingdomDecisionVote(string controllerId, KingdomDecisionVoteData voteData)
        {
            ControllerId = controllerId;
            VoteData = voteData;
        }
    }
}
