using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event sent when player recruits unit
    /// </summary>
    public record TroopCountChanged : IEvent
    {
        public string CharacterId { get; }
        public string PartyId { get; }
        public int Amount { get; }
        public bool isPrisonerRoster { get; }


        public TroopCountChanged(string characterId, int amount, string partyId, bool prisonerRoster)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
            isPrisonerRoster = prisonerRoster;
        }
    }
}