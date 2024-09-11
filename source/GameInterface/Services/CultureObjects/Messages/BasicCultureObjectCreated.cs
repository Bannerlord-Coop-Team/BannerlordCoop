using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects.Messages
{
    internal record BasicCultureObjectCreated : IEvent
    {
        public BasicCultureObject CultureObject { get; }

        public BasicCultureObjectCreated(BasicCultureObject cultureObject)
        {
            CultureObject = cultureObject;
        }
    }
}
