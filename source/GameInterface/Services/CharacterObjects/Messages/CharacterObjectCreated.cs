using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects.Messages
{
    internal record CharacterObjectCreated : IEvent
    {
        public CharacterObject CharacterObject { get; }

        public CharacterObjectCreated(CharacterObject characterObject)
        {
            CharacterObject = characterObject;
        }
    }
}
