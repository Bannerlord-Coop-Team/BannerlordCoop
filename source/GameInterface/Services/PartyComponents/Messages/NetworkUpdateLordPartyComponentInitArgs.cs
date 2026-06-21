using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract]
internal readonly struct NetworkUpdateLordPartyComponentInitArgs : IEvent
{
    [ProtoMember(1)]
    public readonly string LordPartyComponentId;
    [ProtoMember(2)]
    public readonly CampaignVec2 Position;
    [ProtoMember(3)]
    public readonly float SpawnRadius;
    /// <summary>
    /// StringId of the spawn <c>Settlement</c>. Null when the party has no spawn settlement.
    /// </summary>
    [ProtoMember(4)]
    public readonly string SpawnSettlementId;

    public NetworkUpdateLordPartyComponentInitArgs(
        string lordPartyComponentId,
        CampaignVec2 position,
        float spawnRadius,
        string spawnSettlementId)
    {
        LordPartyComponentId = lordPartyComponentId;
        Position = position;
        SpawnRadius = spawnRadius;
        SpawnSettlementId = spawnSettlementId;
    }
}
