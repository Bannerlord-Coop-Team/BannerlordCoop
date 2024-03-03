using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Data;
[ProtoContract(SkipConstructor = true)]
public class HeroCreationData
{
    public HeroCreationData(string heroId)
    {
        HeroStringId = heroId;
    }

    [ProtoMember(1)]
    public string HeroStringId { get; }
}
