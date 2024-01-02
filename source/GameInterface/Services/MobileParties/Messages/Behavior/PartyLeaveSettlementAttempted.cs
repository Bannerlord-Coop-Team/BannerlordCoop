using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a party attempts to leave a settlement
/// </summary>
[BatchLogMessage]
public record PartyLeaveSettlementAttempted : IEvent
{
    public string PartyId { get; }

    public PartyLeaveSettlementAttempted(string partyId)
    {
        PartyId = partyId;
    }
}
