using ProtoBuf;

namespace GameInterface.Services.Inventory.TradeSkills.Data;

[ProtoContract(SkipConstructor = true)]
public class TradeRumorData
{
    [ProtoMember(1)]
    public long RumorEndTime;

    [ProtoMember(2)]
    public string SettlementId;

    [ProtoMember(3)]
    public string ItemObjectId;

    [ProtoMember(4)]
    public int BuyPrice;

    [ProtoMember(5)]
    public int SellPrice;

    public TradeRumorData(
        long rumorEndTime,
        string settlementId,
        string itemObjectId,
        int buyPrice,
        int sellPrice)
    {
        RumorEndTime = rumorEndTime;
        SettlementId = settlementId;
        ItemObjectId = itemObjectId;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
    }
}
