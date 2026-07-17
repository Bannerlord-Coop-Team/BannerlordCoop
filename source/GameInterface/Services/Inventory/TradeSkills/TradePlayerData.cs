using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
    public Dictionary<string, Dictionary<string, TradeSkillCampaignBehavior.ItemTradeData>> PlayerItemsTradeData { get; }

    public TradePlayerData(Dictionary<string, Dictionary<string, TradeSkillCampaignBehavior.ItemTradeData>> playerItemsTradeData)
    {
        PlayerItemsTradeData = playerItemsTradeData;
    }
}
