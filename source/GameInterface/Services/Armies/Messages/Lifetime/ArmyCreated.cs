using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Command to create a new army.
/// </summary>
public record ArmyCreated : ICommand
{
    public ArmyCreationData Data { get; }

    public ArmyCreated(ArmyCreationData data)
    {
        Data = data;
    }
}
