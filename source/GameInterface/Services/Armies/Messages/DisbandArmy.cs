using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages
{
    public record DisbandArmy : ICommand
    {
        public ArmyDeletionData Data { get; }

        public DisbandArmy(ArmyDeletionData armyDeletionData)
        {
            Data = armyDeletionData;
        }
    }
}
