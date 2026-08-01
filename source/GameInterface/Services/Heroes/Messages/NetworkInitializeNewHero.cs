using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
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
    public readonly Equipment CivilianEquipment;

    [ProtoMember(5)]
    public readonly Equipment BattleEquipment;

    public NetworkInitializeNewHero(
        string heroId,
        TextObject firstName,
        TextObject name,
        Equipment civilianEquipment,
        Equipment battleEquipment)
    {
        HeroId = heroId;
        FirstName = firstName;
        Name = name;
        CivilianEquipment = civilianEquipment;
        BattleEquipment = battleEquipment;
    }
}
