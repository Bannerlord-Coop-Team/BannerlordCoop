using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to create an Army
/// </summary>
public class CreateArmy : ICommand
{
    public ArmyCreationData Data { get; }

    public CreateArmy(ArmyCreationData armyCreationData)
    {
        Data = armyCreationData;
    }
}
