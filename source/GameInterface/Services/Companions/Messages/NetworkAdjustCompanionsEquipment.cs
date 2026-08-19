using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Companions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAdjustCompanionsEquipment : ICommand
{
    [ProtoMember(1)]
    public readonly string CompanionHeroId;

    [ProtoMember(2)]
    public readonly string BattleEquipmentId;

    [ProtoMember(3)]
    public readonly string CivilianEquipmentId;

    [ProtoMember(4)]
    public readonly Equipment BattleEquipment;

    [ProtoMember(5)]
    public readonly Equipment CivilianEquipment;

    public NetworkAdjustCompanionsEquipment(
        string companionHeroId,
        string battleEquipmentId,
        string civilianEquipmentId,
        Equipment battleEquipment,
        Equipment civilianEquipment)
    {
        CompanionHeroId = companionHeroId;
        BattleEquipmentId = battleEquipmentId;
        CivilianEquipmentId = civilianEquipmentId;
        BattleEquipment = battleEquipment;
        CivilianEquipment = civilianEquipment;
    }
}
