using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// sent to client
/// </summary>
[BatchLogMessage]
public record ChangeSettlementGarrisonWagePaymentLimit : ICommand
{
    public string SettlementId { get; }
    public int GarrisonWagePaymentLimit { get; }

    public ChangeSettlementGarrisonWagePaymentLimit(string settlementId, int garrisonWagePaymentLimit)
    {
        SettlementId = settlementId;
        GarrisonWagePaymentLimit = garrisonWagePaymentLimit;
    }
}
