using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemComponents.Messages;
internal record ItemComponentCreated(ItemComponent Instance) : IEvent
{
    public ItemComponent Instance { get; } = Instance;
}
