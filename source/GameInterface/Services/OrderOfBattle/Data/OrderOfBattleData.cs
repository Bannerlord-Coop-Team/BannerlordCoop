using ProtoBuf;

namespace GameInterface.Services.OrderOfBattle.Data;

/// <summary>
/// One player's copy of the formation lists in OrderOfBattleCampaignBehavior
/// and NavalOrderOfBattleCampaignBehavior
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class OrderOfBattleData
{
    [ProtoMember(1)]
    public OrderOfBattleFormation[] FieldBattleFormations { get; set; }

    [ProtoMember(2)]
    public OrderOfBattleFormation[] FieldBattleArmyFormations { get; set; }

    [ProtoMember(3)]
    public OrderOfBattleFormation[] SiegeFormations { get; set; }

    [ProtoMember(4)]
    public OrderOfBattleFormation[] SiegeArmyFormations { get; set; }

    [ProtoMember(5)]
    public NavalOrderOfBattleFormation[] NavalBattleFormations { get; set; }

    [ProtoMember(6)]
    public NavalOrderOfBattleFormation[] NavalBattleArmyFormations { get; set; }
}
