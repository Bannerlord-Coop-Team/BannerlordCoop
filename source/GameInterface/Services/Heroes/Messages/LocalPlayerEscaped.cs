using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event sent when a player hero escaped
    /// </summary>
    public record LocalPlayerEscaped : IEvent
    {
        public string HeroId { get; }

        public LocalPlayerEscaped(string heroId)
        {
            HeroId = heroId;
        }
    }
}
