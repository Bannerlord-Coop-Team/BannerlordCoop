using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;
public record ChangeSettlementMilitia : ICommand
{
    public string SettlementId { get; }
    public float Militia { get; }

    public ChangeSettlementMilitia(string settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
