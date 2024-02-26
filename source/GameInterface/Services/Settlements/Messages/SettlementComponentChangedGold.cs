using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record SettlementComponentChangedGold : IEvent
    {
        public string SettlementComponentId { get; set; }
        public int Gold { get; set; }
        public SettlementComponentChangedGold(string id, int gold)
        {
            SettlementComponentId = id;
            Gold = gold;
        }
    }
}
