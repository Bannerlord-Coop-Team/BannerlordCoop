using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ApplyKingdomDecisionVote : ICommand
    {
        public string ClanId { get; }
        public KingdomDecisionVoteData VoteData { get; }

        public ApplyKingdomDecisionVote(string clanId, KingdomDecisionVoteData voteData)
        {
            ClanId = clanId;
            VoteData = voteData;
        }
    }
}
