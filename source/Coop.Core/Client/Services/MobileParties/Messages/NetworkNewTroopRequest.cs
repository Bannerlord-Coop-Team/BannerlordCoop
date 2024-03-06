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
    public record NetworkNewTroopRequest : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId;
        [ProtoMember(2)]
        public string PartyId;
        [ProtoMember(3)]
        public bool IsPrisonRoster;
        [ProtoMember(4)]
        public bool InsertAtFront;
        [ProtoMember(5)]
        public int InsertionIndex;

        public NetworkNewTroopRequest(string characterId, string partyId, bool isPrisonRoster, bool insertAtFront, int insertionIndex)
        {
            CharacterId = characterId;
            PartyId = partyId;
            IsPrisonRoster = isPrisonRoster;
            InsertAtFront = insertAtFront;
            InsertionIndex = insertionIndex;
        }
    }
}