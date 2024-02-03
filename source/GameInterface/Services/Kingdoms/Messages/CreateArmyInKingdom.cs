using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    public class CreateArmyInKingdom : ICommand
    {
        public ArmyCreationData Data { get; }

        public CreateArmyInKingdom(ArmyCreationData armyCreationData)
        {
            Data = armyCreationData;
        }
    }
}
