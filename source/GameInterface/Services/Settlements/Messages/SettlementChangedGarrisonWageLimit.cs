using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;
public record SettlementChangedGarrisonWageLimit : ICommand
{
    public string SettlementId { get; }
    public int GarrisonWagePaymentLimit { get; }

    public SettlementChangedGarrisonWageLimit(string settlementId, int garrisonWagePaymentLimit)
    {
        SettlementId = settlementId;
        GarrisonWagePaymentLimit = garrisonWagePaymentLimit;
    }
}
