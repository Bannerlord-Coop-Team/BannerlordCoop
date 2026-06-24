using Autofac;
using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Caravans.Commands;

internal class CaravansCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// View prohibited kingdoms for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_prohibited_kingdoms", "coop.debug.caravans")]
    public static string ViewProhibitedKingdomsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerProhibitedKingdom in coopSessionProvider.CoopSession.CaravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans)
            {
                if (playerProhibitedKingdom.Key == null || playerProhibitedKingdom.Value == null) continue;

                stringBuilder.AppendLine($"{playerProhibitedKingdom.Key}");
                foreach (var prohibitedKingdom in playerProhibitedKingdom.Value)
                {
                    stringBuilder.AppendLine($"{prohibitedKingdom}");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var prohibitedKingdom in Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>()._prohibitedKingdomsForPlayerCaravans)
            {
                stringBuilder.AppendLine($"{prohibitedKingdom.StringId}");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve prohibited kingdoms";
    }

    /// <summary>
    /// View interacted caravans for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_interacted_caravans", "coop.debug.caravans")]
    public static string ViewInteractedCaravansCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerInteractedCaravan in coopSessionProvider.CoopSession.CaravansPlayerData.PlayerInteractedCaravans)
            {
                if (playerInteractedCaravan.Key == null || playerInteractedCaravan.Value == null) continue;

                stringBuilder.AppendLine($"{playerInteractedCaravan.Key}");
                foreach (var interactedCaravan in playerInteractedCaravan.Value)
                {
                    stringBuilder.AppendLine($"{interactedCaravan.Key} ({interactedCaravan.Value})");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var interactedCaravan in Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>()._interactedCaravans)
            {
                stringBuilder.AppendLine($"{interactedCaravan.Key.StringId} ({(int)interactedCaravan.Value})");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve interacted caravans";
    }

    /// <summary>
    /// View player trade rumor taken caravans for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_taken_trade_rumors", "coop.debug.caravans")]
    public static string ViewTakenTradeRumorsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerTakenTradeRumorCaravan in coopSessionProvider.CoopSession.CaravansPlayerData.PlayerTradeRumorTakenCaravans)
            {
                if (playerTakenTradeRumorCaravan.Key == null || playerTakenTradeRumorCaravan.Value == null) continue;

                stringBuilder.AppendLine($"{playerTakenTradeRumorCaravan.Key}");
                foreach (var takenTradeRumorCaravan in playerTakenTradeRumorCaravan.Value)
                {
                    stringBuilder.AppendLine($"{takenTradeRumorCaravan.Key} ({takenTradeRumorCaravan.Value.NumTicks})");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var takenTradeRumorCaravan in Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>()._tradeRumorTakenCaravans)
            {
                stringBuilder.AppendLine($"{takenTradeRumorCaravan.Key.StringId} ({takenTradeRumorCaravan.Value.NumTicks})");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve trade rumor taken caravans";
    }

    [CommandLineArgumentFunction("view_trade_action_logs", "coop.debug.caravans")]
    public static string ViewTradeActionLogs(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var caravanTradeActionLogs in Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>()._tradeActionLogs)
        {
            // Do not output data for caravans in a settlement, logs only refresh for clients when caravans leave a settlement
            if (caravanTradeActionLogs.Key.CurrentSettlement != null) continue;

            stringBuilder.AppendLine($"{caravanTradeActionLogs.Key.StringId}:");
            foreach (var tradeActionLog in caravanTradeActionLogs.Value)
            {
                stringBuilder.AppendLine($"- " +
                    $"{tradeActionLog.BoughtSettlement?.StringId}, " +
                    $"{tradeActionLog.SoldSettlement?.StringId}, " +
                    $"{tradeActionLog.BuyPrice}, " +
                    $"{tradeActionLog.SellPrice}, " +
                    $"{tradeActionLog.ItemRosterElement.EquipmentElement.Item.StringId}, " +
                    $"{tradeActionLog.BoughtTime.NumTicks}");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve trade action logs";
    }

    [CommandLineArgumentFunction("view_looted_caravans", "coop.debug.caravans")]
    public static string ViewLootedCaravans(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var lootedCaravan in Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>()._lootedCaravans)
        {
            stringBuilder.AppendLine($"{lootedCaravan.Key.StringId} ({lootedCaravan.Value.NumTicks})");
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve looted caravans";
    }
}
