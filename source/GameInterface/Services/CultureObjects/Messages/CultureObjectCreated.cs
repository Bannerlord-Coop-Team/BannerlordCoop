using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CultureObjects.Messages
{
    internal record CultureObjectCreated : IEvent
    {
        public CultureObject CultureObject { get; }

        public CultureObjectCreated(CultureObject cultureObject)
        {
            CultureObject = cultureObject;
        }
    }
}
