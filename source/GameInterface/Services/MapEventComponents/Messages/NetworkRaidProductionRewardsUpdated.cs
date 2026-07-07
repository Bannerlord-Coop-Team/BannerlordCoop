using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRaidProductionRewardsUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string ComponentId;
    [ProtoMember(2)]
    public readonly string[] ItemIds;
    [ProtoMember(3)]
    public readonly float[] Values;
    [ProtoMember(4)]
    public readonly bool WasEverInLootingPhase;
    [ProtoMember(5)]
    public readonly float RaidDamage;
    [ProtoMember(6)]
    public readonly bool HasSettlementState;
    [ProtoMember(7)]
    public readonly float SettlementHitPoints;
    [ProtoMember(8)]
    public readonly float VillageHearth;

    public NetworkRaidProductionRewardsUpdated(
        string componentId,
        string[] itemIds,
        float[] values,
        bool wasEverInLootingPhase,
        float raidDamage,
        bool hasSettlementState,
        float settlementHitPoints,
        float villageHearth)
    {
        ComponentId = componentId;
        ItemIds = itemIds;
        Values = values;
        WasEverInLootingPhase = wasEverInLootingPhase;
        RaidDamage = raidDamage;
        HasSettlementState = hasSettlementState;
        SettlementHitPoints = settlementHitPoints;
        VillageHearth = villageHearth;
    }
}