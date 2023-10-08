using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event to add hero to party
    /// </summary>
    public record HeroAddedToParty : IEvent
    {
        public string HeroId { get; }
        public string PartyId { get; }
        public bool ShowNotification { get; }

        public HeroAddedToParty(string heroId, string partyId, bool showNotification)
        {
            HeroId = heroId;
            PartyId = partyId;
            ShowNotification = showNotification;
        }
    }
}