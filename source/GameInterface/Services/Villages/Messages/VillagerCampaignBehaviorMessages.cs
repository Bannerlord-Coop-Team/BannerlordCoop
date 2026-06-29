using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

public readonly struct DeleteExpiredLootedVillagers : IEvent
{
    public DeleteExpiredLootedVillagers() { }
}
