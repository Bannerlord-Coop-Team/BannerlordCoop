using ProtoBuf;

namespace GameInterface.Services.Villages.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct VillageHostileActionCooldownData
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly long CooldownUntilTicks;

    public VillageHostileActionCooldownData(string settlementId, long cooldownUntilTicks)
    {
        SettlementId = settlementId;
        CooldownUntilTicks = cooldownUntilTicks;
    }
}