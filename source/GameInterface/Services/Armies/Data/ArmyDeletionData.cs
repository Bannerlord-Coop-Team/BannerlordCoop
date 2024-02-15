using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Data;


/// <summary>
/// Data required for deleting an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyDeletionData
{
    public ArmyDeletionData(Army army, Army.ArmyDispersionReason reason)
    {
        ArmyId = army.GetStringId();
        Reason = reason.ToString();
    }

    [ProtoMember(1)]
    public string ArmyId { get; }
    [ProtoMember(2)]
    public string Reason { get; }
}
