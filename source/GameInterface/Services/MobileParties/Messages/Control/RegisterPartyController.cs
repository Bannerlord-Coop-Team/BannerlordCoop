using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control;

public record RegisterPartyController : ICommand
{
    public string OwnerId { get; }
    public string PartyId { get; }

    public RegisterPartyController(string ownerId, string partyId)
    {
        OwnerId = ownerId;
        PartyId = partyId;
    }
}