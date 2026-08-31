using Common;
using GameInterface.CoopSessionData;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Inventory.TradeSkills.Commands;

internal class TradeSkillCommands
{
    /// <summary>
    /// View trade data for all players on server and for current player on client
    /// </summary>
    public static string ViewPlayerTradeDataCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerTradeData in coopSessionProvider.CoopSession.TradePlayerData.PlayerItemsTradeData)
            {
                if (playerTradeData.Key == null || playerTradeData.Value == null) continue;

                stringBuilder.AppendLine($"{playerTradeData.Key}");
                foreach (var itemIdTradeData in playerTradeData.Value)
                {
                    stringBuilder.AppendLine($"{itemIdTradeData.Key} (Total Purchased: {itemIdTradeData.Value.Item2}, Average price: {itemIdTradeData.Value.Item1})");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var itemTradeData in Campaign.Current.GetCampaignBehavior<TradeSkillCampaignBehavior>().ItemsTradeData)
            {
                stringBuilder.AppendLine($"{itemTradeData.Key.StringId} (Total Purchased: {itemTradeData.Value.NumItemsPurchased}, Average price: {itemTradeData.Value.AveragePrice})");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve player trade data";
    }

    /// <summary>
    /// View trade rumors for all players on server and for current player on client
    /// </summary>
    public static string ViewPlayerTradeRumorsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerTradeRumors in coopSessionProvider.CoopSession.TradePlayerData.PlayerTradeRumors)
            {
                if (playerTradeRumors.Key == null || playerTradeRumors.Value == null) continue;

                stringBuilder.AppendLine($"{playerTradeRumors.Key}");
                foreach (var rumorData in playerTradeRumors.Value)
                {
                    stringBuilder.AppendLine($"{rumorData.ItemObjectId} at {rumorData.SettlementId} (expiring in {new CampaignTime(rumorData.RumorEndTime).RemainingHoursFromNow} hours) rumored to:");
                    stringBuilder.AppendLine($"     Buy at: {rumorData.BuyPrice}");
                    stringBuilder.AppendLine($"     Sell at: {rumorData.SellPrice}");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var tradeRumor in Campaign.Current.GetCampaignBehavior<TradeRumorsCampaignBehavior>()._tradeRumors)
            {
                stringBuilder.AppendLine($"{tradeRumor.ItemCategory.StringId} at {tradeRumor.Settlement.StringId} (expiring in {tradeRumor.RumorEndTime.RemainingHoursFromNow} hours) rumored to:");
                stringBuilder.AppendLine($"     Buy at: {tradeRumor.BuyPrice}");
                stringBuilder.AppendLine($"     Sell at: {tradeRumor.SellPrice}");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve player rumors data";
    }

    /// <summary>
    /// View entered settlements trade data for all players on server and for current player on client
    /// </summary>
    public static string ViewPlayerEnteredSettlementsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerEnteredSettlement in coopSessionProvider.CoopSession.TradePlayerData.PlayerEnteredSettlements)
            {
                if (playerEnteredSettlement.Key == null || playerEnteredSettlement.Value == null) continue;

                stringBuilder.AppendLine($"{playerEnteredSettlement.Key}");
                foreach (var enteredSettlementData in playerEnteredSettlement.Value)
                {
                    stringBuilder.AppendLine($"{enteredSettlementData.Key} {new CampaignTime(enteredSettlementData.Value).ElapsedHoursUntilNow} hours ago.");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var enteredSettlement in Campaign.Current.GetCampaignBehavior<TradeRumorsCampaignBehavior>()._enteredSettlements)
            {
                stringBuilder.AppendLine($"{enteredSettlement.Key.StringId} {enteredSettlement.Value.ElapsedHoursUntilNow} hours ago.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve player trade data";
    }
}
