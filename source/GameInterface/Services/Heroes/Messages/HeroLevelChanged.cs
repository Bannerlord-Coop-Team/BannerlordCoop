using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for Level
    /// </summary>
    public record HeroLevelChanged : IEvent
    {
        public int HeroLevel { get; }
        public string HeroId { get; }

        public HeroLevelChanged(int heroLevel, string heroId)
        {
            HeroLevel = heroLevel;
            HeroId = heroId;
        }
    }
}
