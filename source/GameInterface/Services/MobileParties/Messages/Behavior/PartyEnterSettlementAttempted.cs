using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a party attempts to enter a settlement
/// </summary>
[DontLogMessage]
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
