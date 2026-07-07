using Common.Messaging;
using GameInterface.Services.Villages.Data;

namespace GameInterface.Services.Villages.Messages;

public readonly struct VillageHostileActionCooldownsChanged : IEvent
{
    public readonly VillageHostileActionCooldownData[] Cooldowns;

    public VillageHostileActionCooldownsChanged(VillageHostileActionCooldownData[] cooldowns)
    {
        Cooldowns = cooldowns;
    }
}