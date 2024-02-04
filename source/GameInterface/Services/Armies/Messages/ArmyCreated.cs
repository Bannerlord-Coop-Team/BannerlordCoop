using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when an Army is created
/// </summary>
public record ArmyCreated : IEvent
{
    public ArmyCreationData Data { get; }

    public ArmyCreated(ArmyCreationData armyCreationData)
    {
        Data = armyCreationData;
    }
}
