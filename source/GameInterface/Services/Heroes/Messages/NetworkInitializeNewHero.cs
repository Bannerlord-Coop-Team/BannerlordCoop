using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkInitializeNewHero : IEvent
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly TextObject FirstName;

    [ProtoMember(3)]
    public readonly TextObject Name;

    [ProtoMember(4)]
    public readonly string CivilianEquipmentId;

    [ProtoMember(5)]
    public readonly string BattleEquipmentId;

    [ProtoMember(6)]
    public readonly Equipment CivilianEquipment;

    [ProtoMember(7)]
    public readonly Equipment BattleEquipment;

    public NetworkInitializeNewHero(
        string heroId,
        TextObject firstName,
        TextObject name,
        string civilianEquipmentId,
        string battleEquipmentId,
        Equipment civilianEquipment,
        Equipment battleEquipment)
    {
        HeroId = heroId;
        FirstName = firstName;
        Name = name;
        CivilianEquipmentId = civilianEquipmentId;
        BattleEquipmentId = battleEquipmentId;
        CivilianEquipment = civilianEquipment;
        BattleEquipment = battleEquipment;
    }
}
