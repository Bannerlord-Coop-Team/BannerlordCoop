using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _heroState
    /// </summary>
    public record HeroStateChanged : IEvent
    {
        public int HeroState { get; }
        public string HeroId { get; }

        public HeroStateChanged(int heroState, string heroId)
        {
            HeroState = heroState;
            HeroId = heroId;
        }
    }
}