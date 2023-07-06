using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Request entry to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSettlementEnterRequest : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId;
        [ProtoMember(2)]
        public string PartyId;

        public NetworkSettlementEnterRequest(string settlementId, string partyId)
        {
            SettlementId = settlementId;
            PartyId = partyId;
        }
    }
}
