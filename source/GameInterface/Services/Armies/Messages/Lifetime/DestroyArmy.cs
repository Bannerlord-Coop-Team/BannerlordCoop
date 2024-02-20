using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Command to destroy a army.
/// </summary>
public record DestroyArmy : ICommand
{
    public ArmyDestructionData Data { get; }

    public DestroyArmy(ArmyDestructionData data)
    {
        Data = data;
    }
}
