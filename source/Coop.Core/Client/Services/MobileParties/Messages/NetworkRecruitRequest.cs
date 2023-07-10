using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Request to recruit troops from client
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRecruitRequest : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId;
        [ProtoMember(2)]
        public int Amount;
        [ProtoMember(3)]
        public string PartyId;

        public NetworkRecruitRequest(string characterId, int amount, string partyId)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
        }
    }
}
