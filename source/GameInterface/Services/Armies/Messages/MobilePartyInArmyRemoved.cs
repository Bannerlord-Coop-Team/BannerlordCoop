using Common.Messaging;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is removed from an Army
/// </summary>
public record MobilePartyInArmyRemoved : IEvent
{
    public List<string> MobilePartyIds { get; }
    public string ArmyId { get; }

    public MobilePartyInArmyRemoved(List<string> mobilePartyIds, string armyId)
    {
        MobilePartyIds = mobilePartyIds;
        ArmyId = armyId;
    }

}
