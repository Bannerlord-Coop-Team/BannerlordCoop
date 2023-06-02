using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    public record UpdateMobilePartyControl : ICommand
    {
        public string PartyId { get; }

        public PartyControlAction Action { get; }

        public UpdateMobilePartyControl(string partyId, PartyControlAction action)
        {
            PartyId = partyId;
            Action = action;
        }
    }

    public enum PartyControlAction
    {
        Grant,
        Revoke
    }
}
