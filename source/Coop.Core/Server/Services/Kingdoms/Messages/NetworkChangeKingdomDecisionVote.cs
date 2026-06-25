using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeKingdomDecisionVote : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public KingdomDecisionVoteData VoteData { get; }

        public NetworkChangeKingdomDecisionVote(string clanId, KingdomDecisionVoteData voteData)
        {
            ClanId = clanId;
            VoteData = voteData;
        }
    }
}
