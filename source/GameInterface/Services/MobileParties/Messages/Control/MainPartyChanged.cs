using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control
{
    public class MainPartyChanged : IEvent
    {
        public string NewPartyId { get; }

        public MainPartyChanged(string newPartyId)
        {
            NewPartyId = newPartyId;
        }
    }
}
