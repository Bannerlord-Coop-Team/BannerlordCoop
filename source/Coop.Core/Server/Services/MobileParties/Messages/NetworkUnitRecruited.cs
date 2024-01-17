using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Network command for recruited unit
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUnitRecruited : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId;
        [ProtoMember(2)]
        public int Amount;
        [ProtoMember(3)]
        public string PartyId;
        [ProtoMember(4)]
        public bool IsPrisonerRoster;

        public NetworkUnitRecruited(string characterId, int amount, string partyId, bool prisonerRoster)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
            IsPrisonerRoster = prisonerRoster;
        }
    }
}
