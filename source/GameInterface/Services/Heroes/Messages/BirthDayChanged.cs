using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _birthDay
    /// </summary>
    public record BirthDayChanged : IEvent
    {
        public long BirthDay { get; }
        public string HeroId { get; }

        public BirthDayChanged(long birthDay, string heroId)
        {
            BirthDay = birthDay;
            HeroId = heroId;
        }
    }
}