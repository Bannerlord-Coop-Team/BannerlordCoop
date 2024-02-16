using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Audit;

[ProtoContract(SkipConstructor = true)]
internal record ArmyAuditData
{
    [ProtoMember(1)]
    public string StringId { get; }

    [ProtoMember(2)]
    public string Name { get; }

    public ArmyAuditData(Army army)
    {
        StringId = army.GetStringId();
        Name = army.Name?.ToString();
    }
}
