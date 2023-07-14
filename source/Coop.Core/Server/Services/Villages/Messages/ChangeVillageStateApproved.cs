using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Villages.Messages
{
    /// <summary>
    /// Change village state approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record ChangeVillageStateApproved : ICommand
    {
        [ProtoMember(1)]
        public string VillageId { get; }
        [ProtoMember(2)]
        public int NewState { get; }
        [ProtoMember(3)]
        public string PartyId { get; }

        public ChangeVillageStateApproved(string villageId, int newState, string partyId)
        {
            VillageId = villageId;
            NewState = newState;
            PartyId = partyId;
        }
    }
}
