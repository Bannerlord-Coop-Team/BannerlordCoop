using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ArmyInKingdomCreated : ICommand
    {

        public string KingdomId { get; }
        public string ArmyLeaderId { get; }
        public string TargetSettlement { get; }
        public string SelectedArmyType { get; }

        public ArmyInKingdomCreated(string kingdomId, string armyLeaderId, string targetSettlement, string selectedArmyType)
        {
            KingdomId = kingdomId;
            ArmyLeaderId = armyLeaderId;
            TargetSettlement = targetSettlement;
            SelectedArmyType = selectedArmyType;
        }
    }
}
