using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService.Messages
{
    internal record CraftingRemoved : IEvent
    {
        public Crafting crafting;

        public CraftingRemoved(Crafting crafting)
        {
            this.crafting = crafting;
        }
    }
}
