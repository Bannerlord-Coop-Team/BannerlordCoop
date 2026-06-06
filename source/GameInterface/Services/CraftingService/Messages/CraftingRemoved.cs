using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService.Messages
{
    internal record CraftingRemoved : IEvent
    {
        public Crafting Crafting { get; }

        public CraftingRemoved(Crafting crafting)
        {
            Crafting = crafting;
        }
    }
}
