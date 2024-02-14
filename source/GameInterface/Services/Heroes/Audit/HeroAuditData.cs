using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Audit;

[ProtoContract(SkipConstructor = true)]
internal record HeroAuditData
{
    [ProtoMember(1)]
    public string StringId { get; }

    [ProtoMember(2)]
    public string Name { get; }

    public HeroAuditData(Hero h)
    {
        StringId = h.StringId;
        Name = h.Name?.ToString();
    }
}
