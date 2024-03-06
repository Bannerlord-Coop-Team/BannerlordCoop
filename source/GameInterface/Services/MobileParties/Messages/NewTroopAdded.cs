using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event sent when player recruits unit
    /// </summary>
    public record NewTroopAdded : IEvent
    {
        public string CharacterId { get; }
        public string PartyId { get; }
        public bool isPrisonerRoster { get; }
        public bool InsertAtFront { get; }
        public int InsertionIndex { get; }


        public NewTroopAdded(string characterId, string partyId, bool prisonerRoster, bool insertAtFront, int insertionIndex)
        {
            CharacterId = characterId;
            PartyId = partyId;
            isPrisonerRoster = prisonerRoster;
            InsertAtFront = insertAtFront;
            InsertionIndex = insertionIndex;
        }
    }
}