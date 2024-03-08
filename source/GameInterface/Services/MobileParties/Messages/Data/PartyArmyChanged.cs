using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Data;

/// <summary>
/// Event for when the army of a party has changed.
/// </summary>
public record PartyArmyChanged : IEvent
{
    public PartyArmyChangeData Data { get; }

    public PartyArmyChanged(PartyArmyChangeData data)
    {
        Data = data;
    }
}
