using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MapEvents.Messages
{
    /// <summary>
    /// Request entry to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementEnterRequest : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementEnterRequest(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}
