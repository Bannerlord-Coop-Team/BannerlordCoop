using ProtoBuf;

namespace GameInterface.Services.Inventory.Data;

[ProtoContract(SkipConstructor = true)]
public struct ItemObjectData
{
    [ProtoMember(1)]
    public string ItemObjectId { get; set; }
    [ProtoMember(2)]
    public string ItemModifierId { get; set; }
    [ProtoMember(3)]
    public bool ItemModifierNull { get; set; }

    public ItemObjectData(string itemObjectId, string itemModifierId, bool itemModifierNull = false)
    {
        ItemObjectId = itemObjectId;
        ItemModifierId = itemModifierId;
        ItemModifierNull = itemModifierNull;
    }
}

[ProtoContract(SkipConstructor = true)]
public struct ItemRosterElementData
{
    [ProtoMember(1)]
    public ItemObjectData ItemObjectData { get; set; }
    [ProtoMember(2)]
    public int Amount { get; set; }

    public ItemRosterElementData(ItemObjectData itemObjectData, int amount)
    {
        ItemObjectData = itemObjectData;
        Amount = amount;
    }
}

