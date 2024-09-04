using Common.Messaging;
using GameInterface.Services.Armies.Data;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is added to an Army
/// </summary>
public record MobilePartyInArmyAdded : IEvent
{
    public ArmyAddPartyData Data { get; }

    public MobilePartyInArmyAdded(ArmyAddPartyData data)
    {
        Data = data;
    }
}
