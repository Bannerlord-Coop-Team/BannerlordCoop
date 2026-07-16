using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct EquipmentSurrogate
{
    [ProtoMember(1)]
    public Equipment.EquipmentType EquipmentType { get; set; }

    [ProtoMember(2)]
    public EquipmentElement[] ItemSlots { get; set; }

    public EquipmentSurrogate(Equipment equipment)
    {
        EquipmentType = equipment._equipmentType;
        ItemSlots = equipment._itemSlots;
    }

    public static implicit operator EquipmentSurrogate(Equipment equipment)
    {
        return new EquipmentSurrogate(equipment);
    }

    public static implicit operator Equipment(EquipmentSurrogate surrogate)
    {
        var equipment = new Equipment(surrogate.EquipmentType);
        for (int i = 0; i < Equipment.EquipmentSlotLength; i++)
        {
            equipment._itemSlots[i] = surrogate.ItemSlots[i];
        }

        return equipment;
    }
}

