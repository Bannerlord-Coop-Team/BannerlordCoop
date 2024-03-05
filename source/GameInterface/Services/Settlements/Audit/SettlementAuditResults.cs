using Common.Messaging;

namespace GameInterface.Services.Settlements.Audit;

/// <summary>
/// The Results of the Servers Audit
/// </summary>
public record SettlementAuditResults : IEvent
{
    public SettlementAuditData[] Data { get; }
    public string ServerAuditResults { get; }

    public SettlementAuditResults(SettlementAuditData[] data, string serverAuditResults)
    {
        Data = data;
        ServerAuditResults = serverAuditResults;
    }
}
