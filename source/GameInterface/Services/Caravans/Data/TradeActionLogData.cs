using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Caravans.Data;

[ProtoContract(SkipConstructor = true)]
public struct TradeActionLogData
{
    [ProtoMember(1)]
    public string LegacyBoughtSettlementId { get; set; }

    [ProtoMember(2)]
    public int BuyPrice { get; set; }

    [ProtoMember(3)]
    public int SellPrice { get; set; }

    [ProtoMember(4)]
    public CaravanTradeItemData ItemRosterElement { get; set; }

    [ProtoMember(5)]
    public string LegacySoldSettlementId { get; set; }

    [ProtoMember(6)]
    public CampaignTime BoughtTime { get; set; }

    [ProtoMember(7)]
    public uint BoughtSettlementId { get; set; }

    [ProtoMember(8)]
    public uint SoldSettlementId { get; set; }

    public TradeActionLogData(
        uint boughtSettlementId,
        int buyPrice,
        int sellPrice,
        ItemRosterElement itemRosterElement,
        uint soldSettlementId,
        CampaignTime boughtTime)
    {
        LegacyBoughtSettlementId = null;
        BoughtSettlementId = boughtSettlementId;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
        ItemRosterElement = new CaravanTradeItemData
        {
            ItemObjectId = itemRosterElement.EquipmentElement.Item?.StringId,
            Amount = itemRosterElement.Amount,
            ItemModifierId = itemRosterElement.EquipmentElement.ItemModifier?.StringId,
        };
        LegacySoldSettlementId = null;
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
