using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdateEquipment : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly EquipmentType EquipmentType;

    [ProtoMember(3)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(4)]
    public readonly EquipmentIndex EquipmentIndex;

    public UpdateEquipment(
        string heroId,
        EquipmentType equipmentType,
        EquipmentElement equipmentElement,
        EquipmentIndex equipmentIndex)
    {
        HeroId = heroId;
        EquipmentType = equipmentType;
        EquipmentElement = equipmentElement;
        EquipmentIndex = equipmentIndex;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdateEquipmentClients : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly EquipmentType EquipmentType;

    [ProtoMember(3)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(4)]
    public readonly EquipmentIndex EquipmentIndex;

    public UpdateEquipmentClients(
        string heroId,
        EquipmentType equipmentType,
        EquipmentElement equipmentElement,
        EquipmentIndex equipmentIndex)
    {
        HeroId = heroId;
        EquipmentType = equipmentType;
        EquipmentElement = equipmentElement;
        EquipmentIndex = equipmentIndex;
    }
}