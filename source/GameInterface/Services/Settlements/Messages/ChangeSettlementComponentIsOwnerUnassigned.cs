using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    public record ChangeSettlementComponentIsOwnerUnassigned : ICommand
    {
        public string SettlementComponentId { get; set; }
        public bool IsOwnerUnassigned { get; set; }
        public ChangeSettlementComponentIsOwnerUnassigned(string settlementComponentId, bool isOwnerUnassigned)
        {
            SettlementComponentId = settlementComponentId;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
