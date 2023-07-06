using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Allow entry to a settlement.
    /// </summary>
    public record PartySettlementEnter : ICommand
    {
        public string SettlementId;
        public string PartyId;

        public PartySettlementEnter(string settlementId, string partyId)
        {
            SettlementId = settlementId;
            PartyId = partyId;
        }
    }
}