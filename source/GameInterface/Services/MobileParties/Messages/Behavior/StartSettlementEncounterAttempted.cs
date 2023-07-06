using Common.Messaging;

namespace GameInterface.Services.MobileParties.Patches
{
    public record StartSettlementEncounterAttempted : IEvent
    {
        public string PartyId { get; }
        public string SettlementId { get; }

        public StartSettlementEncounterAttempted(
            string partyId,
            string settlementId)
        {
            PartyId = partyId;
            SettlementId = settlementId;
        }
    }
}