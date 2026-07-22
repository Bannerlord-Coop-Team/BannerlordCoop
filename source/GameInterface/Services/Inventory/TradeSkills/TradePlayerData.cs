using ProtoBuf;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory.TradeSkills;

/// <summary>
/// TradeSkillCampaignBehavior.ItemsTradeData only assumes one player.
/// Need to manage separately for all players
/// Will need be expanded in future to also contain trade rumors
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class TradePlayerData
{
    // Dictionary<PlayerHeroId, <ItemObjectId, ItemTradeData>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, Tuple<float, int>>> PlayerItemsTradeData { get; }

    public TradePlayerData(Dictionary<string, Dictionary<string, Tuple<float, int>>> playerItemsTradeData)
    {
        PlayerItemsTradeData = playerItemsTradeData;
    }
}
