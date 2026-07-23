using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Audit;

[ProtoContract(SkipConstructor = true)]
internal record HeroAuditData
{
    [ProtoMember(1)]
    public string Id { get; }

    [ProtoMember(2)]
    public string Name { get; }

    public HeroAuditData(string id, Hero h)
    {
        Id = id;
        Name = h.Name?.ToString();
    }
}
