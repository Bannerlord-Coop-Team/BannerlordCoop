using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record SettlementComponentChangedOwner : IEvent
    {
        public string SettlementComponentId { get; set; }
        public string OwnerId { get; set; }
        public SettlementComponentChangedOwner(string id, string ownerId)
        {
            SettlementComponentId = id;
            OwnerId = ownerId;
        }
    }
}
