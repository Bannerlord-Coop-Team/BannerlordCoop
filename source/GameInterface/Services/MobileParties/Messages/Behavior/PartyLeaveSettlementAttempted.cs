using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a party attempts to leave a settlement
/// </summary>
public record PartyLeaveSettlementAttempted : IEvent
{
    public string PartyId { get; }

    public PartyLeaveSettlementAttempted(string partyId)
    {
        PartyId = partyId;
    }
}
