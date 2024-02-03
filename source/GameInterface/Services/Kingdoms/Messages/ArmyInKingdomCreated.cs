using Common.Messaging;
using GameInterface.Services.Armies.Data;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ArmyInKingdomCreated : ICommand
    {
        public ArmyCreationData Data { get; }

        public ArmyInKingdomCreated(ArmyCreationData armyCreationData)
        {
            Data = armyCreationData;
        }
    }
}
