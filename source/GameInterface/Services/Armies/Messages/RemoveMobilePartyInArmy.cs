using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record RemoveMobilePartyInArmy : ICommand
    {
        public string MobilePartyId { get; }
        public string LeaderMobilePartyId { get; }

        public RemoveMobilePartyInArmy(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }

    }
}
