using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MapEvents
{
    /// <summary>
    /// Event fired when the local player enters a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementEntered : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementEntered(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}
