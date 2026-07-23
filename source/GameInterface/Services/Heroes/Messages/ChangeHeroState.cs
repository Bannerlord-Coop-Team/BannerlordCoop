using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Client publish for _heroState
    /// </summary>
    public record ChangeHeroState : ICommand
    {
        public int HeroState { get; }
        public string HeroId { get; }

        public ChangeHeroState(int heroState, string heroId)
        {
            HeroState = heroState;
            HeroId = heroId;
        }
    }
}
