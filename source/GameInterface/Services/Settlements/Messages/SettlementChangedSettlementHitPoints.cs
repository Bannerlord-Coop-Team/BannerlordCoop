using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the Settlement Hit points changes.
/// </summary>
public record SettlementChangedSettlementHitPoints : ICommand
{

    public string SettlementId { get; }
    public float SettlementHitPoints { get; }

    public SettlementChangedSettlementHitPoints(string settlementId, float settlementHitPoints)
    {
        SettlementId = settlementId;
        SettlementHitPoints = settlementHitPoints;
    }
}
