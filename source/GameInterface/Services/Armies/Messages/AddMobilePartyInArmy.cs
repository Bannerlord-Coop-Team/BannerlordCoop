using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a MobileParty to an Army
/// </summary>
public record AddMobilePartyInArmy : ICommand
{
    public string MobilePartyId { get; }
    public string ArmyId { get; }

    public AddMobilePartyInArmy(string mobilePartyId, string armyId)
    {
        MobilePartyId = mobilePartyId;
        ArmyId = armyId;
    }

}
