using Common;
using GameInterface.CoopSessionData;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Inventory.TradeSkills.Commands;

internal class TradeSkillCommands
{
    /// <summary>
    /// View trade data for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_player_trade_data", "coop.debug.inventory")]
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
                    stringBuilder.AppendLine($"{itemIdTradeData.Key} (Total Purchased: {itemIdTradeData.Value.Item1}, Average price: {itemIdTradeData.Value.Item2})");
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
}
