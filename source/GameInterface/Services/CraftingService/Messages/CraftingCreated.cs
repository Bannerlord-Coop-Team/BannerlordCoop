using Common.Messaging;

namespace GameInterface.Services.CraftingService.Messages
{
    internal record CraftingCreated(CraftingCreatedData Data) : IEvent
    {
        public CraftingCreatedData Data { get; } = Data;
    }
}
