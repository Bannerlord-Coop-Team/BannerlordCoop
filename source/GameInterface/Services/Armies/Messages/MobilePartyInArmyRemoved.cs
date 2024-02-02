using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record MobilePartyInArmyRemoved : ICommand
    {
        public string MobilePartyId { get; }
        public string LeaderMobilePartyId { get; }

        public MobilePartyInArmyRemoved(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }

    }
}
