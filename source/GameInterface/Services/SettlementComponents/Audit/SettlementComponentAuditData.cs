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
    public string StringId { get; }
    [ProtoMember(2)]
    public string Name { get; }


    public SettlementComponentAuditData(SettlementComponent settlementcomponent)
    {
        StringId = settlementcomponent.StringId;
        Name = settlementcomponent.Name?.ToString();
    }
}
