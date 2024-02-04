using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is added to an Army
/// </summary>
public record MobilePartyInArmyAdded : IEvent
{
    public string MobilePartyId { get; }
    public string ArmyId { get; }

    public MobilePartyInArmyAdded(string mobilePartyId, string armyId)
    {
        MobilePartyId = mobilePartyId;
        ArmyId = armyId;
    }

}
