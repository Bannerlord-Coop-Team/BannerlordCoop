using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Event fired when the local player enters a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementEntered : IEvent
    {
        [ProtoMember(1)]
        public string SettlementId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementEntered(string settlementId, string partyId)
        {
            SettlementId = settlementId;
            PartyId = partyId;
        }
    }
}
