using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

public record PartyLeaveSettlementAttempted : IEvent
{
    public string PartyId { get; }

    public PartyLeaveSettlementAttempted(string partyId)
    {
        PartyId = partyId;
    }
}
