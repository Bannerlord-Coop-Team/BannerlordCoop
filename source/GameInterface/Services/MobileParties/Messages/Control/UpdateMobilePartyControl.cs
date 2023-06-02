using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control
{
    public record UpdateMobilePartyControl : ICommand
    {
        public string PartyId { get; }

        public bool IsRevocation { get; }

        public UpdateMobilePartyControl(string partyId, bool isRevocation = false)
        {
            PartyId = partyId;
            IsRevocation = isRevocation;
        }
    }
}
