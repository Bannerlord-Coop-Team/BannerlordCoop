using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to remove a MobileParty from an Army
/// </summary>
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
