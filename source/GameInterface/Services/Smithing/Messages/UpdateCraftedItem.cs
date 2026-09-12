using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct UpdateCraftedItem : IEvent
{
    public readonly ItemObject CraftedItemObject;

    public UpdateCraftedItem(ItemObject craftedItemObject)
    {
        CraftedItemObject = craftedItemObject;
    }
}
