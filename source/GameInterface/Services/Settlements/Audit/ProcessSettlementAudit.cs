using Common.Messaging;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// ProcessAuditSettlement when requested
/// </summary>
public record ProcessSettlementAudit : IAuditRequest
{
    public SettlementAuditData[] Data { get; }

    IAuditData[] IAuditRequest.Data => Data;

    public ProcessSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
