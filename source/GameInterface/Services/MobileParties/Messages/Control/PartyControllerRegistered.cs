using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control;

public record PartyControllerRegistered : IEvent
{
    public string OwnerId { get; }
    public string PartyId { get; }

    public PartyControllerRegistered(string ownerId, string partyId)
    {
        OwnerId = ownerId;
        PartyId = partyId;
    }
}