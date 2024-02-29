using Common.Messaging;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// Request  to get auditdata
/// </summary>
public record SettlementGetAuditData : IEvent
{
    public SettlementAuditData[] Data { get; }

    public SettlementGetAuditData(SettlementAuditData[] data)
    {
        Data = data;
    }
}
