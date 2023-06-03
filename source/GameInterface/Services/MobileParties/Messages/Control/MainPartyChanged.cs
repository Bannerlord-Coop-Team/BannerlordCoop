using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control
{
    /// <summary>
    /// Event fired when the local main player has changed.
    /// </summary>
    public class MainPartyChanged : IEvent
    {
        public string NewPartyId { get; }

        public MainPartyChanged(string newPartyId)
        {
            NewPartyId = newPartyId;
        }
    }
}
