using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    public record AddNewTroop : IEvent
    {
        public string CharacterId { get; }
        public string PartyId { get; }
        public bool IsPrisonRoster { get; }
        public bool InsertAtFront { get; }
        public int InsertionIndex { get; }

        public AddNewTroop(string characterId, string partyId, bool isPrisonRoster, bool insertAtFront, int insertionIndex)
        {
            CharacterId = characterId;
            PartyId = partyId;
            IsPrisonRoster = isPrisonRoster;
            InsertAtFront = insertAtFront;
            InsertionIndex = insertionIndex;
        }
    }
}