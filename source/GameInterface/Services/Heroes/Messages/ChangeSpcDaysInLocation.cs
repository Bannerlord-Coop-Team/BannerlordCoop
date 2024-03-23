using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Client publish for SpcDaysInLocation
    /// </summary>
    public record ChangeSpcDaysInLocation : ICommand
    {
        public int Days { get; }
        public string HeroId { get; }

        public ChangeSpcDaysInLocation(int days, string heroId)
        {
            Days = days;
            HeroId = heroId;
        }
    }
}
