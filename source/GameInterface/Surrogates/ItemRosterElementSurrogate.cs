using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;


namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemRosterElementSurrogate
{
    [ProtoMember(1)]
    public string ItemObjectId { get; set; }

    [ProtoMember(2)]
    public int Amount { get; set; }

    [ProtoMember(3)]
    public string ItemModifierId { get; set; }

    public ItemRosterElementSurrogate(ItemRosterElement itemRosterElement)
    {
        ItemObjectId = itemRosterElement.EquipmentElement.Item?.StringId;
        Amount = itemRosterElement.Amount;
        ItemModifierId = itemRosterElement.EquipmentElement.ItemModifier?.StringId;
    }

    public static implicit operator ItemRosterElementSurrogate(ItemRosterElement itemRosterElement)
    {
        return new ItemRosterElementSurrogate(itemRosterElement);
    }

    public static implicit operator ItemRosterElement(ItemRosterElementSurrogate surrogate)
    {
        var item = string.IsNullOrEmpty(surrogate.ItemObjectId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemObject>(surrogate.ItemObjectId);
        var modifier = string.IsNullOrEmpty(surrogate.ItemModifierId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemModifier>(surrogate.ItemModifierId);

        return new ItemRosterElement(item, surrogate.Amount, modifier);
    }
}

