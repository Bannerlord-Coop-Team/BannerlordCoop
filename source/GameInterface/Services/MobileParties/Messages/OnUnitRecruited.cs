using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event sent when player recruits unit
    /// </summary>
    public record OnUnitRecruited : IEvent
    {
        public string CharacterId { get; }
        public string PartyId { get; }
        public int Amount { get; }

        public OnUnitRecruited(string characterId, int amount, string partyId)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
        }
    }
}
