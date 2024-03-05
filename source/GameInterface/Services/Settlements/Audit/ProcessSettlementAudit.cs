using Common.Messaging;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// ProcessAuditSettlement when requested
/// </summary>
public record ProcessSettlementAudit : ICommand
{
    public SettlementAuditData[] Data { get; }

    public ProcessSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
