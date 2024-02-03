using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to destroy an Army
/// </summary>
public record DestroyArmy : ICommand
{
    public ArmyDeletionData Data { get; }

    public DestroyArmy(ArmyDeletionData armyDeletionData)
    {
        Data = armyDeletionData;
    }
}
