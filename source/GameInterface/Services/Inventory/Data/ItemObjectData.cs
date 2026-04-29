using ProtoBuf;

namespace GameInterface.Services.Inventory.Data;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ItemObjectData
{
    [ProtoMember(1)]
    public readonly string ItemObjectId;
    [ProtoMember(2)]
    public readonly string ItemModifierId;
    [ProtoMember(3)]
    public readonly bool ItemModifierNull;

    public ItemObjectData(string itemObjectId, string itemModifierId, bool itemModifierNull = false)
    {
        ItemObjectId = itemObjectId;
        ItemModifierId = itemModifierId;
        ItemModifierNull = itemModifierNull;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ItemRosterElementData
{
    [ProtoMember(1)]
    public readonly ItemObjectData ItemObjectData;
    [ProtoMember(2)]
    public readonly int Amount;

    public ItemRosterElementData(ItemObjectData itemObjectData, int amount)
    {
        ItemObjectData = itemObjectData;
        Amount = amount;
    }
}

