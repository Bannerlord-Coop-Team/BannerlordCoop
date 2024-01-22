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
    public record NetworkNewTroopAdded : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId;
        [ProtoMember(2)]
        public string PartyId;
        [ProtoMember(3)]
        public bool IsPrisonerRoster;
        [ProtoMember(4)]
        public bool InsertAtFront;
        [ProtoMember(5)]
        public int InsertionIndex;

        public NetworkNewTroopAdded(string characterId, string partyId, bool isPrisonerRoster, bool insertAtFront, int insertionIndex)
        {
            CharacterId = characterId;
            PartyId = partyId;
            IsPrisonerRoster = isPrisonerRoster;
            InsertAtFront = insertAtFront;
            InsertionIndex = insertionIndex;
        }
    }
}
