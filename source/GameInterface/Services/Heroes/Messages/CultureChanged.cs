using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for Culture
    /// </summary>
    public record CultureChanged : IEvent
    {
        public string CultureStringId { get; }
        public string HeroId { get; }

        public CultureChanged(string cultureStringId, string heroId)
        {
            CultureStringId = cultureStringId;
            HeroId = heroId;
        }
    }
}