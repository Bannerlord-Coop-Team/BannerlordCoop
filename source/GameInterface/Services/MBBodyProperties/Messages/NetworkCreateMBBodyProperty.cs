using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.MBBodyProperties.Messages;
internal class MBBodyPropertyCreated : IEvent
{
    public MBBodyPropertyCreated(MBBodyProperty instance)
    {
        Instance = instance;
    }
    public MBBodyProperty Instance { get; }
}
