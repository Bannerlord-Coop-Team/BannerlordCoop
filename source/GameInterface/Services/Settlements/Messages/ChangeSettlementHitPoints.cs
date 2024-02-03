using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Le tthe client know to change Settlement hit points
/// </summary>
public record ChangeSettlementHitPoints : ICommand
{
    public string SettlementId { get; }
    public float SettlementHitPoints { get; }

    public ChangeSettlementHitPoints(string settlementId, float settlementHitPoints)
    {
        SettlementId = settlementId;
        SettlementHitPoints = settlementHitPoints;
    }
}
