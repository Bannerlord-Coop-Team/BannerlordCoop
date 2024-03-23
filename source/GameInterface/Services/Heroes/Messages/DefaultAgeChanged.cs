using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _defaultAge
    /// </summary>
    public record DefaultAgeChanged : IEvent
    {
        public float Age { get; }
        public string HeroId { get; }

        public DefaultAgeChanged(float age, string heroId)
        {
            Age = age;
            HeroId = heroId;
        }
    }
}