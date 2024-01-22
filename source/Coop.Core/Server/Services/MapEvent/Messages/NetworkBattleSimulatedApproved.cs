using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MapEvent.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBattleSimulatedApproved : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }
        [ProtoMember(2)]
        public int Side { get; }
        [ProtoMember(3)]
        public float Advantage { get; }

        public NetworkBattleSimulatedApproved(string partyId, int side, float advantage)
        {
            PartyId = partyId;
            Side = side;
            Advantage = advantage;
        }
    }
}
