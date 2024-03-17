using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _firstName
    /// </summary>
    public record FirstNameChanged : IEvent
    {
        public string NewName { get; }
        public string HeroId { get; }

        public FirstNameChanged(string newName, string heroId)
        {
            NewName = newName;
            HeroId = heroId;
        }
    }
}