using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MapEvents
{
    /// <summary>
    /// Allow entry to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementEnterAllowed : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementEnterAllowed(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}
