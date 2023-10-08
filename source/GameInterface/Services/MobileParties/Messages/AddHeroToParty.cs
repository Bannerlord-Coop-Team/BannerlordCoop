using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event to add hero to party
    /// </summary>
    public record AddHeroToParty : IEvent
    {
        public string HeroId { get; }
        public string PartyId { get; }
        public bool ShowNotification { get; }

        public AddHeroToParty(string heroId, string partyId, bool showNotification)
        {
            HeroId = heroId;
            PartyId = partyId;
            ShowNotification = showNotification;
        }
    }
}