using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAddNewTroop : ICommand
    {
        [ProtoMember(1)]
        public string CharacterId { get; }
        [ProtoMember(2)]
        public string PartyId { get; }
        [ProtoMember(3)]
        public bool IsPrisonRoster { get; }
        [ProtoMember(4)]
        public bool InsertAtFront { get; }
        [ProtoMember(5)]
        public int InsertionIndex { get; }

        public NetworkAddNewTroop(string characterId, string partyId, bool isPrisonRoster, bool insertAtFront, int insertionIndex)
        {
            CharacterId = characterId;
            PartyId = partyId;
            IsPrisonRoster = isPrisonRoster;
            InsertAtFront = insertAtFront;
            InsertionIndex = insertionIndex;
        }
    }
}