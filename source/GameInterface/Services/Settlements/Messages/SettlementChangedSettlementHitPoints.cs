using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the Settlement Hit points changes.
/// </summary>
[BatchLogMessage]
public record SettlementChangedSettlementHitPoints : IEvent
{

    public string SettlementId { get; }
    public float SettlementHitPoints { get; }

    public SettlementChangedSettlementHitPoints(string settlementId, float settlementHitPoints)
    {
        SettlementId = settlementId;
        SettlementHitPoints = settlementHitPoints;
    }
}
