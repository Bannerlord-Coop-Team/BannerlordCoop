using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
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
