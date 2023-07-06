using Common.Messaging;

namespace GameInterface.Services.MobileParties.Patches
{
    public record StartSettlementEncounter : ICommand
    {
        public string AttackerPartyId { get; }
        public string SettlementId { get; }

        public StartSettlementEncounter(
            string attackerPartyId,
            string settlementId)
        {
            AttackerPartyId = attackerPartyId;
            SettlementId = settlementId;
        }
    }
}