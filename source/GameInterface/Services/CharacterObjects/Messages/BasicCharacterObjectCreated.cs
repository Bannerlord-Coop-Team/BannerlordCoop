using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterObjects.Messages
{
    internal record BasicCharacterObjectCreated : IEvent
    {
        public BasicCharacterObject BasicCharacterObject { get; }

        public BasicCharacterObjectCreated(BasicCharacterObject basicCharacterObject)
        {
            BasicCharacterObject = basicCharacterObject;
        }
    }
}
