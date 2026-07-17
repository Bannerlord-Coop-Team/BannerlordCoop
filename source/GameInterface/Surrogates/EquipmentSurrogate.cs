using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal class EquipmentSurrogate
{
    [ProtoMember(1)]
    public Equipment.EquipmentType EquipmentType { get; set; }

    [ProtoMember(2)]
    public EquipmentElement[] ItemSlots { get; set; }

    // Both conversions MUST pass null through: protobuf-net converts the target member's INITIAL value into
    // the surrogate before merging wire data, and spawn contracts are SkipConstructor, so that value is null
    // on every deserialize — a throwing conversion NREs on every wire receive of an Equipment field.
    public static implicit operator EquipmentSurrogate(Equipment equipment)
    {
        if (equipment is null)
            return null!;

        return new EquipmentSurrogate
        {
            EquipmentType = equipment._equipmentType,
            ItemSlots = equipment._itemSlots,
        };
    }

    public static implicit operator Equipment(EquipmentSurrogate surrogate)
    {
        if (surrogate is null)
            return null!;

        var equipment = new Equipment(surrogate.EquipmentType);
        if (surrogate.ItemSlots is not null)
        {
            int count = Math.Min(Equipment.EquipmentSlotLength, surrogate.ItemSlots.Length);
            for (int i = 0; i < count; i++)
            {
                equipment._itemSlots[i] = surrogate.ItemSlots[i];
            }
        }

        return equipment;
    }
}
