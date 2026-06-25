using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record KingdomDecisionVoteChanged : IEvent
    {
        public string ClanId { get; }
        public KingdomDecisionVoteData VoteData { get; }

        public KingdomDecisionVoteChanged(string clanId, KingdomDecisionVoteData voteData)
        {
            ClanId = clanId;
            VoteData = voteData;
        }
    }
}
