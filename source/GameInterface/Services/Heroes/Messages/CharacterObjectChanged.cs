using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _characterObject
    /// </summary>
    public record CharacterObjectChanged : IEvent
    {
        public string CharacterObjectId { get; }
        public string HeroId { get; }

        public CharacterObjectChanged(string characterObjectId, string heroId)
        {
            CharacterObjectId = characterObjectId;
            HeroId = heroId;
        }
    }
}