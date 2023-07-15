using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control;

public record PartyControllerRemoved : IEvent
{
    public string OwnerId { get; }
    public string PartyId { get; }

    public PartyControllerRemoved(string ownerId, string partyId)
    {
        OwnerId = ownerId;
        PartyId = partyId;
    }
}