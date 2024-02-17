using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Data;


/// <summary>
/// Data required for deleting an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyDestructionData
{
    [ProtoMember(1)]
    public string StringId { get; }
    [ProtoMember(2)]
    public short Reason { get; }

    public ArmyDestructionData(Army army, Army.ArmyDispersionReason reason)
    {
        StringId = army.GetStringId();
        Reason = (short)reason;
    }
}
