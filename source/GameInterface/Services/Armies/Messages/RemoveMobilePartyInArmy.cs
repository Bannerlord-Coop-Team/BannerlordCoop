using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to remove a MobileParty from an Army
/// </summary>
public record RemoveMobilePartyInArmy : ICommand
{
    public List<string> MobilePartyIds { get; }
    public string ArmyId { get; }

    public RemoveMobilePartyInArmy(List<string> mobilePartyIds, string armyId)
    {
        MobilePartyIds = mobilePartyIds;
        ArmyId = armyId;
    }

}
