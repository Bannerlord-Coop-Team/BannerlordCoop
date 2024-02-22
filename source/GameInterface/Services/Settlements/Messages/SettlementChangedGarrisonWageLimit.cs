using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Settlement Client changed garrison wage
/// </summary>
[BatchLogMessage]
public record SettlementChangedGarrisonWageLimit : IEvent
{
    public string SettlementId { get; }
    public int GarrisonWagePaymentLimit { get; }

    public SettlementChangedGarrisonWageLimit(string settlementId, int garrisonWagePaymentLimit)
    {
        SettlementId = settlementId;
        GarrisonWagePaymentLimit = garrisonWagePaymentLimit;
    }
}
