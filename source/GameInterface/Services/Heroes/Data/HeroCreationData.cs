using Coop.Mod.Extentions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Data;
[ProtoContract(SkipConstructor = true)]
public class HeroCreationData
{
    public HeroCreationData(CharacterObject template, int age, CampaignTime birthday, Settlement bornSettlement, string heroId)
    {
        TemplateStringId = template.StringId;
        Age = age;
        Birthday = birthday.GetNumTicks();
        BornSettlementId = bornSettlement.StringId;
        HeroStringId = heroId;
    }

    [ProtoMember(1)]
    public string TemplateStringId { get; }
    [ProtoMember(2)]
    public string BornSettlementId { get; }
    [ProtoMember(3)]
    public int Age { get; }
    [ProtoMember(4)]
    public long Birthday { get; }
    [ProtoMember(5)]
    public string HeroStringId { get; }
}
