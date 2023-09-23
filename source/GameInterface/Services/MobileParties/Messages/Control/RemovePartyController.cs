using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control;

public record RemovePartyController : ICommand
{
    public string OwnerId { get; }
    public string PartyId { get; }

    public RemovePartyController(string ownerId, string partyId)
    {
        OwnerId = ownerId;
        PartyId = partyId;
    }
}