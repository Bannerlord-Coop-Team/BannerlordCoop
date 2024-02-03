using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a MobileParty to an Army
/// </summary>
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
