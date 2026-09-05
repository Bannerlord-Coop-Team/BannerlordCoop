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
    public CaravanTradeItemData ItemRosterElement { get; set; }

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
        ItemRosterElement = new CaravanTradeItemData
        {
            ItemObjectId = itemRosterElement.EquipmentElement.Item?.StringId,
            Amount = itemRosterElement.Amount,
            ItemModifierId = itemRosterElement.EquipmentElement.ItemModifier?.StringId,
        };
        SoldSettlementId = soldSettlementId;
        BoughtTime = boughtTime;
    }
}

/// <summary>
/// Retains native item identities until the caravan update reaches the game thread.
/// Field numbers match the existing ItemRosterElement surrogate wire contract.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public struct CaravanTradeItemData
{
    [ProtoMember(1)]
    public string ItemObjectId { get; set; }

    [ProtoMember(2)]
    public int Amount { get; set; }

    [ProtoMember(3)]
    public string ItemModifierId { get; set; }
}
