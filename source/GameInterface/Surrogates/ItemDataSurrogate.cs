using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemDataSurrogate
{
    [ProtoMember(1)]
    public float Supply { get; set; }

    [ProtoMember(2)]
    public float Demand { get; set; }

    [ProtoMember(3)]
    public int InStore { get; set; }

    [ProtoMember(4)]
    public int InStoreValue { get; set; }

    public ItemDataSurrogate(ItemData itemData)
    {
        Supply = itemData.Supply;
        Demand = itemData.Demand;
        InStore = itemData.InStore;
        InStoreValue = itemData.InStoreValue;
    }

    public static implicit operator ItemDataSurrogate(ItemData itemData)
    {
        return new ItemDataSurrogate(itemData);
    }

    public static implicit operator ItemData(ItemDataSurrogate surrogate)
    {
        return new ItemData(surrogate.Supply, surrogate.Demand, surrogate.InStore, surrogate.InStoreValue);
    }
}
