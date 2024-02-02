using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record MobilePartyInArmyAdded : ICommand
    {
        public string MobilePartyId { get; }
        public string LeaderMobilePartyId { get; }

        public MobilePartyInArmyAdded(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }

    }
}
