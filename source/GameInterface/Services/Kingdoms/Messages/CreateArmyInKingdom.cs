using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    public class CreateArmyInKingdom : ICommand
    {
        public string KingdomId { get; }
        public string ArmyLeaderId { get; }
        public string TargetSettlement { get; }
        public string SelectedArmyType { get; }

        public CreateArmyInKingdom(string kingdomId, string armyLeaderId, string targetSettlement, string selectedArmyType)
        {
            KingdomId = kingdomId;
            ArmyLeaderId = armyLeaderId;
            TargetSettlement = targetSettlement;
            SelectedArmyType = selectedArmyType;
        }
    }
}
