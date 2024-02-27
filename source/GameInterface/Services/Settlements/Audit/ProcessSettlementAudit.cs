using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Audit;
public record ProcessSettlementAudit : ICommand
{
    public SettlementAuditData[] Data { get; }

    public ProcessSettlementAudit(SettlementAuditData[] data)
    {
        Data = data;
    }
}
