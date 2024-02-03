using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages
{
    public record ArmyDisbanded : ICommand
    {
        public ArmyDeletionData Data { get; }

        public ArmyDisbanded(ArmyDeletionData armyDeletionData)
        {
            Data = armyDeletionData;
        }

    }
}
