using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects.Messages
{
    internal class BasicCultureCreated : IEvent
    {
        public BasicCultureObject CultureObject { get; }

        public BasicCultureCreated(BasicCultureObject cultureObject)
        {
            CultureObject = cultureObject;
        }
    }
}
