using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record ChangeSettlementComponentOwner : ICommand
    {
        public string SettlementComponentId { get; set; }
        public string OwnerId { get; set; }
        public ChangeSettlementComponentOwner(string settlementComponentId, string ownerId)
        {
            SettlementComponentId = settlementComponentId;
            OwnerId = ownerId;
        }
    }
}
