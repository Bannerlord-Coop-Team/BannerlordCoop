using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Data;

/// <summary>
/// Data required for creating an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyCreationData
{
    [ProtoMember(1)]
    public string StringId { get; }
    internal ArmyCreationData(Army instance)
    {
        StringId = instance.GetStringId();
    }
}
