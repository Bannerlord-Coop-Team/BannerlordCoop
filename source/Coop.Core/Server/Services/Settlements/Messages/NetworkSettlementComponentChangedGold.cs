using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSettlementComponentChangedGold : IEvent
    {
        [ProtoMember(1)]
        public string SettlementComponentId { get; set; }
        [ProtoMember(2)]
        public int Gold { get; set; }
        public NetworkSettlementComponentChangedGold(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
