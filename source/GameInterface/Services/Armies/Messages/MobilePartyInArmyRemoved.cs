using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is removed from an Army
/// </summary>
public record MobilePartyInArmyRemoved : IEvent
{
    public string MobilePartyId { get; }
    public string LeaderMobilePartyId { get; }

    public MobilePartyInArmyRemoved(string mobilePartyId, string leaderMobilePartyId)
    {
        MobilePartyId = mobilePartyId;
        LeaderMobilePartyId = leaderMobilePartyId;
    }

}
