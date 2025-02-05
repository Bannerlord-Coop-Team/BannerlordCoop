using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for LastTimeStampForActivity
    /// </summary>
    public record LastTimeStampChanged : IEvent
    {
        public int LastTimeStampForActivity { get; }
        public string HeroId { get; }

        public LastTimeStampChanged(int lastTimeStampForActivity, string heroId)
        {
            LastTimeStampForActivity = lastTimeStampForActivity;
            HeroId = heroId;
        }
    }
}
