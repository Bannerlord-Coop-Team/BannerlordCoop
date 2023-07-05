using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUnitRecruited : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId;
        [ProtoMember(2)]
        public int Amount;
        [ProtoMember(3)]
        public string PartyId;

        public NetworkUnitRecruited(string characterId, int amount, string partyId)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
        }
    }
}
