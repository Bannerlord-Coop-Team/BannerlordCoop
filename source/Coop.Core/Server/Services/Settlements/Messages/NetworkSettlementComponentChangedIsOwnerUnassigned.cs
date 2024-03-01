using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSettlementComponentChangedIsOwnerUnassigned : IEvent
    {
        [ProtoMember(1)]
        public string SettlementComponentId { get; set; }
        [ProtoMember(2)]
        public bool IsOwnerUnassigned { get; set; }
        public NetworkSettlementComponentChangedIsOwnerUnassigned(string settlementComponentId, bool isOwnerUnassigned)
        {
            SettlementComponentId = settlementComponentId;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
