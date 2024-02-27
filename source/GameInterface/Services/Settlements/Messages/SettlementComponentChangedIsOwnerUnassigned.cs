using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record SettlementComponentChangedIsOwnerUnassigned : IEvent
    {
        public string SettlementComponentId { get; set; }
        public bool IsOwnerUnassigned { get; set; }
        public SettlementComponentChangedIsOwnerUnassigned(string id, bool isOwnerUnassigned)
        {
            SettlementComponentId = id;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
