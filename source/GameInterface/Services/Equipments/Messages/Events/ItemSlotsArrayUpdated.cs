using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record ItemSlotsArrayUpdated(Equipment Instance, ItemObject Item, ItemModifier ItemModifier, int Index) : IEvent
{
    public Equipment Instance { get; } = Instance;
    public ItemObject Item { get; } = Item;
    public ItemModifier ItemModifier { get; } = ItemModifier;
    public int Index { get; } = Index;
}
