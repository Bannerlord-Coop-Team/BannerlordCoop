using Common.Messaging;

public class NetworkChangeCreateArmyInKingdom : IEvent
{
    public string KingdomId { get; }
    public string ArmyLeaderId { get; }
    public string TargetSettlement { get; }
    public string SelectedArmyType { get; }

    public NetworkChangeCreateArmyInKingdom(string kingdomId, string armyLeaderId, string targetSettlement, string selectedArmyType)
    {
        KingdomId = kingdomId;
        ArmyLeaderId = armyLeaderId;
        TargetSettlement = targetSettlement;
        SelectedArmyType = selectedArmyType;
    }
}