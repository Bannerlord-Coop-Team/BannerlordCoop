using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _power
    /// </summary>
    public record PowerChanged : IEvent
    {
        public float Power { get; }
        public string HeroId { get; }

        public PowerChanged(float power, string heroId)
        {
            Power = power;
            HeroId = heroId;
        }
    }
}