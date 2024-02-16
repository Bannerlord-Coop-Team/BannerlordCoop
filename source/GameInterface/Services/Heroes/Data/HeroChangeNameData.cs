using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Data;

[ProtoContract(SkipConstructor = true)]
public record HeroChangeNameData
{
    public HeroChangeNameData(Hero instance, TextObject name, TextObject firstName)
    {
        HeroStringId = instance.StringId;
        FullName = name.Value;
        FirstName = firstName.Value;
    }

    [ProtoMember(1)]
    public string HeroStringId { get; }
    [ProtoMember(2)]
    public string FullName { get; }
    [ProtoMember(3)]
    public string FirstName { get; }
}
