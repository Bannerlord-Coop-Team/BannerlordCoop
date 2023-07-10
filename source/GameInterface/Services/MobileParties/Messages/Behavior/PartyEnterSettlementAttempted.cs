using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

public record PartyEnterSettlementAttempted : IEvent
{
    public string SettlementId { get; }
    public string PartyId { get; }

    public PartyEnterSettlementAttempted(string settlementId, string partyId)
    {
        SettlementId = settlementId;
        PartyId = partyId;
    }
}
