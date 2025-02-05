using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Client publish for Level
    /// </summary>
    public record ChangeHeroLevel : ICommand
    {
        public int HeroLevel { get; }
        public string HeroId { get; }

        public ChangeHeroLevel(int heroLevel, string heroId)
        {
            HeroLevel = heroLevel;
            HeroId = heroId;
        }
    }
}