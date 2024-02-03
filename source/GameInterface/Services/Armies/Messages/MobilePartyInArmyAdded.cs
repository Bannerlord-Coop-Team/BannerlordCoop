using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is added to an Army
/// </summary>
public record MobilePartyInArmyAdded : IEvent
{
    public string MobilePartyId { get; }
    public string LeaderMobilePartyId { get; }

    public MobilePartyInArmyAdded(string mobilePartyId, string leaderMobilePartyId)
    {
        MobilePartyId = mobilePartyId;
        LeaderMobilePartyId = leaderMobilePartyId;
    }

}
