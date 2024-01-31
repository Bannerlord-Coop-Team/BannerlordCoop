using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record AddMobilePartyInArmy : ICommand
    {
        public string MobilePartyId { get; }
        public string LeaderMobilePartyId { get; }

        public AddMobilePartyInArmy(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }

    }
}
