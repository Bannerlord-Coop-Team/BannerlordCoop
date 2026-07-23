using ProtoBuf;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

[ProtoContract(SkipConstructor = true)]
internal readonly struct EquipmentData
{
    [ProtoMember(1)]
    public readonly EquipmentType EquipmentType;

    [ProtoMember(2)]
    public readonly EquipmentElement[] ItemSlots;

    public EquipmentData(
        EquipmentType equipmentType,
        EquipmentElement[] itemSlots)
    {
        EquipmentType = equipmentType;
        ItemSlots = itemSlots;
    }
}