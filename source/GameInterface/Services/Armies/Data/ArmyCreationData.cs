using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Data;


[ProtoContract(SkipConstructor = true)]
public class ArmyCreationData
{
    internal ArmyCreationData(Kingdom instance, Hero armyLeader, Settlement targetSettlement, Army.ArmyTypes selectedArmyType, string newArmyId)
    {
        KingdomStringId = instance.StringId;
        LeaderHeroStringId = armyLeader.StringId;
        TargetSettlementStringId = targetSettlement.StringId;
        SelectedArmyType = (short)selectedArmyType;
        ArmyStringId = newArmyId;
    }

    [ProtoMember(1)]
    public string KingdomStringId { get; }
    [ProtoMember(2)]
    public string LeaderHeroStringId { get; }
    [ProtoMember(3)]
    public string TargetSettlementStringId { get; }
    [ProtoMember(4)]
    public short SelectedArmyType { get; }
    [ProtoMember(5)]
    public string ArmyStringId { get; }
}
