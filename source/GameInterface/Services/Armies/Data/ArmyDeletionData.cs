using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Data;


/// <summary>
/// Data required for deleting an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class ArmyDeletionData
{
    public ArmyDeletionData(Army army, Army.ArmyDispersionReason reason)
    {
        ArmyId = army.GetStringId();
        Reason = (short)reason;
    }

    [ProtoMember(1)]
    public string ArmyId { get; }
    [ProtoMember(2)]
    public short Reason { get; }
}
