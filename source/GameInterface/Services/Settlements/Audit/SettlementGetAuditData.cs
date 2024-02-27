using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Audit;
public record SettlementGetAuditData : IEvent
{
    public SettlementAuditData[] Data { get; }

    public SettlementGetAuditData(SettlementAuditData[] data)
    {
        Data = data;
    }
}
