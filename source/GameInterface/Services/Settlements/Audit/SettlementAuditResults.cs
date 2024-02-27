using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Audit;
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
