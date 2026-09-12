using GameInterface.Services.Inventory.TradeSkills.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory.TradeSkills;

/// <summary>
/// The following assume only one player
/// TradeSkillCampaignBehavior.ItemsTradeData
/// TradeRumorsCampaignBehavior._tradeRumors
/// TradeRumorsCampaignBehavior._enteredSettlements
/// Settlement.BribePaid
/// Need to manage separately for all players
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class TradePlayerData
{
    // Dictionary<PlayerHeroId, Dictionary<ItemObjectId, ItemTradeData>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, Tuple<float, int>>> PlayerItemsTradeData { get; }

    // Dictionary<PlayerHeroId, List<TradeRumorData>>
    [ProtoMember(2)]
    public Dictionary<string, List<TradeRumorData>> PlayerTradeRumors { get; }

    // Dictionary<PlayerHeroId, Dictionary<SettlementId, CampaignTimeNumTicks>>
    [ProtoMember(3)]
    public Dictionary<string, Dictionary<string, long>> PlayerEnteredSettlements { get; }

    // Dictionary<PlayerHeroId, Dictionary<SettlementId, BribePaid>>
    [ProtoMember(4)]
    public Dictionary<string, Dictionary<string, int>> PlayerSettlementBribePaid { get; }

    public TradePlayerData(
        Dictionary<string, Dictionary<string, Tuple<float, int>>> playerItemsTradeData,
        Dictionary<string, List<TradeRumorData>> playerTradeRumors,
        Dictionary<string, Dictionary<string, long>> playerEnteredSettlements,
        Dictionary<string, Dictionary<string, int>> playerSettlementBribePaid)
    {
        PlayerItemsTradeData = playerItemsTradeData ?? new();
        PlayerTradeRumors = playerTradeRumors ?? new();
        PlayerEnteredSettlements = playerEnteredSettlements ?? new();
        PlayerSettlementBribePaid = playerSettlementBribePaid ?? new();
    }
}
