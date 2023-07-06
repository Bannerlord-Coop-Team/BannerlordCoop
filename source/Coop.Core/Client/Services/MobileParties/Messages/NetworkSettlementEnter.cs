using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Event fired when the local player enters a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkSettlementEnter : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId;
        [ProtoMember(2)]
        public string PartyId;

        public NetworkSettlementEnter(string settlementId, string partyId)
        {
            SettlementId = settlementId;
            PartyId = partyId;
        }
    }
}
