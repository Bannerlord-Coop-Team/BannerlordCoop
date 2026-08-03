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
    public readonly Equipment BattleEquipment;

    [ProtoMember(3)]
    public readonly Equipment CivilianEquipment;

    public NetworkAdjustCompanionsEquipment(
        string companionHeroId,
        Equipment battleEquipment,
        Equipment civilianEquipment)
    {
        CompanionHeroId = companionHeroId;
        BattleEquipment = battleEquipment;
        CivilianEquipment = civilianEquipment;
    }
}
