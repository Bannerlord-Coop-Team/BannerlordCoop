using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    public record UnitRecruitGranted : IEvent
    {
        public string CharacterId { get; }
        public int Amount { get; }
        public string PartyId { get; }

        public UnitRecruitGranted (string characterId, int amount, string partyId)
        {
            CharacterId = characterId;
            Amount = amount;
            PartyId = partyId;
        }
    }
}