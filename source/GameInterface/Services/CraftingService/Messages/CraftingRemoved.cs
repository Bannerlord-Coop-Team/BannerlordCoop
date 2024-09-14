using Common.Messaging;

namespace GameInterface.Services.CraftingService.Messages
{
    internal record CraftingRemoved : IEvent
    {
        public string craftingId;

        public CraftingRemoved(string craftingId)
        {
            this.craftingId = craftingId;
        }
    }
}
