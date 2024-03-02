using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record ChangeSettlementComponentGold : ICommand
    {
        public string SettlementComponentId { get; set; }
        public int Gold { get; set; }
        public ChangeSettlementComponentGold(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
