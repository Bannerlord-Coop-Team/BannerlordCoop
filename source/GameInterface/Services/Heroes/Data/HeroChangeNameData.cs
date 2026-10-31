using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Data;

[ProtoContract(SkipConstructor = true)]
public record HeroChangeNameData
{
    [ProtoMember(1)]
    public readonly string HeroStringId;

    [ProtoMember(2)]
    public readonly TextObject FullName;

    [ProtoMember(3)]
    public readonly TextObject FirstName;

    public HeroChangeNameData(Hero instance, TextObject name, TextObject firstName)
    {
        HeroStringId = instance.StringId;
        FullName = name;
        FirstName = firstName;
    }
}
