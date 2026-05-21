using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;


namespace GameInterface.Surrogates;

[ProtoContract]
internal struct EquipmentElementSurrogate
{
    [ProtoMember(1)]
    public string ItemId { get; set; }

    [ProtoMember(2)]
    public string ItemModifierId { get; set; }

    public EquipmentElementSurrogate(EquipmentElement equipmentElement)
    {
        ItemId = equipmentElement.Item?.StringId;
        ItemModifierId = equipmentElement.ItemModifier?.StringId;

    }
    public static implicit operator EquipmentElementSurrogate(EquipmentElement equipmentElement)
    {
        return new EquipmentElementSurrogate(equipmentElement);
    }

    public static implicit operator EquipmentElement(EquipmentElementSurrogate surrogate)
    {
        var item = MBObjectManager.Instance.GetObject<ItemObject>(surrogate.ItemId);
        var modifier = string.IsNullOrEmpty(surrogate.ItemModifierId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemModifier>(surrogate.ItemModifierId);

        return new EquipmentElement(item, modifier);
    }
}

