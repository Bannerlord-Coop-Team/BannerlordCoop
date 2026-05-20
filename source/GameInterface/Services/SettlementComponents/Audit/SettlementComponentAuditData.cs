using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Audit data for SettlementComponent.
/// </summary>

[ProtoContract(SkipConstructor = true)]
internal record SettlementComponentAuditData
{
    [ProtoMember(1)]
    public string NetworkId { get; }
    [ProtoMember(2)]
    public string StringId { get; }
    [ProtoMember(3)]
    public string Name { get; }

    public SettlementComponentAuditData(string networkId, string stringId, string name)
    {
        NetworkId = networkId;
        StringId = stringId;
        Name = name;
    }
}