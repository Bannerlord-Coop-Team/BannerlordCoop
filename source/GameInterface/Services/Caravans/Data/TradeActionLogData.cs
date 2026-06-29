using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Caravans.Data;

[ProtoContract(SkipConstructor = true)]
public struct TradeActionLogData
{
    [ProtoMember(1)]
    public string BoughtSettlementId { get; set; }

    [ProtoMember(2)]
    public int BuyPrice { get; set; }

    [ProtoMember(3)]
    public int SellPrice { get; set; }

    [ProtoMember(4)]
    public ItemRosterElement ItemRosterElement { get; set; }

    [ProtoMember(5)]
    public string SoldSettlementId { get; set; }

    [ProtoMember(6)]
    public CampaignTime BoughtTime { get; set; }

    public TradeActionLogData(
        string boughtSettlementId,
        int buyPrice,
        int sellPrice,
        ItemRosterElement itemRosterElement,
        string soldSettlementId,
        CampaignTime boughtTime)
    {
        BoughtSettlementId = boughtSettlementId;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
        ItemRosterElement = itemRosterElement;
        SoldSettlementId = soldSettlementId;
        BoughtTime = boughtTime;
    }
}
