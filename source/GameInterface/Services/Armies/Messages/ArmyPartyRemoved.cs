using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is removed from an Army
/// </summary>
public record ArmyPartyRemoved : IEvent
{
    public ArmyRemovePartyData Data { get; }

    public ArmyPartyRemoved(ArmyRemovePartyData data)
    {
        Data = data;
    }
}
