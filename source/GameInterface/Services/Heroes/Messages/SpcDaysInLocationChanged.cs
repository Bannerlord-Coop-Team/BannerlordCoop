using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for SpcDaysInLocation
    /// </summary>
    public record SpcDaysInLocationChanged : IEvent
    {
        public int Days { get; }
        public string HeroId { get; }

        public SpcDaysInLocationChanged(int days, string heroId)
        {
            Days = days;
            HeroId = heroId;
        }
    }
}
