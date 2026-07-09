using Common.Messaging;
using GameInterface.Services.Villages.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageHostileActionCooldowns : ICommand
{
    [ProtoMember(1)]
    public readonly VillageHostileActionCooldownData[] Cooldowns;

    public NetworkVillageHostileActionCooldowns(VillageHostileActionCooldownData[] cooldowns)
    {
        Cooldowns = cooldowns;
    }
}