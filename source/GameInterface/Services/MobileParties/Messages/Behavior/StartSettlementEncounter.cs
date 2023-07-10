using Common.Messaging;

namespace GameInterface.Services.MobileParties.Patches
{
    public record StartSettlementEncounter : ICommand
    {
        public string PartyId { get; }
        public string SettlementId { get; }

        public StartSettlementEncounter(
            string attackerPartyId,
            string settlementId)
        {
            PartyId = attackerPartyId;
            SettlementId = settlementId;
        }
    }
}