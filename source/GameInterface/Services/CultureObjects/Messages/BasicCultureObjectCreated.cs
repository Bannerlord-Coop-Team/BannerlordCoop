using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.CultureObjects.Messages;

internal record BasicCultureObjectCreated : IEvent
{
    public BasicCultureObject BasicCultureObject { get; }

    public BasicCultureObjectCreated(BasicCultureObject basicCultureObject)
    {
        BasicCultureObject = basicCultureObject;
    }
}
