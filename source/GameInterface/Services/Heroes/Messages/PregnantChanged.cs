using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for IsPregnant
    /// </summary>
    public record PregnantChanged : IEvent
    {
        public bool IsPregnant { get; }
        public string HeroId { get; }

        public PregnantChanged(bool isPregnant, string heroId)
        {
            IsPregnant = isPregnant;
            HeroId = heroId;
        }
    }
}