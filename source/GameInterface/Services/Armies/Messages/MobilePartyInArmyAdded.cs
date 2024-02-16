using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is added to an Army
/// </summary>
public record MobilePartyInArmyAdded : IEvent
{
    public List<string> MobilePartyListId { get; }
    public string ArmyId { get; }

    public MobilePartyInArmyAdded(List<string> mobilePartyListId, string armyId)
    {
        MobilePartyListId = mobilePartyListId;
        ArmyId = armyId;
    }

}
