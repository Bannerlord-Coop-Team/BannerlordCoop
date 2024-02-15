using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a MobileParty to an Army
/// </summary>
public record AddMobilePartyInArmy : ICommand
{
    public List<string> MobilePartyListId { get; }
    public string ArmyId { get; }

    public AddMobilePartyInArmy(List<string> mobilePartyListId, string armyId)
    {
        MobilePartyListId = mobilePartyListId;
        ArmyId = armyId;
    }

}
