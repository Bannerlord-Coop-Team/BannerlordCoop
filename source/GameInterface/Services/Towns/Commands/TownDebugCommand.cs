using Autofac;
using Common.Extensions;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class TownDebugCommand
{
    private static readonly Func<Town, Town.SellLog[]> getSoldItems = typeof(Town).GetField("_soldItems", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Town, Town.SellLog[]>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    // coop.debug.town.list
    /// <summary>
    /// Lists all the towns
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the towns</returns>
    [CommandLineArgumentFunction("list_towns", "coop.debug.town")]
    public static string ListTowns(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown).ToList();

        settlements.ForEach((settlement) =>
        {
            Town t = settlement.Town;
            stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", t.StringId, t.Name));
        });

        return stringBuilder.ToString();
    }

    // coop.debug.town.list
    /// <summary>
    /// Lists all the items
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the items</returns>
    [CommandLineArgumentFunction("list_items", "coop.debug.town")]
    public static string ListItems(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        List<ItemCategory> items = Campaign.Current.ObjectManager.GetObjectTypeList<ItemCategory>().ToList();

        items.ForEach((item) =>
        {
            stringBuilder.Append(string.Format("ID: '{0}'\n", item.StringId));
        });

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Gets information on a specific town.
    /// </summary>
    /// <param name="args">town ID to lookup</param>
    /// <returns>Information regarding the town.</returns>
    [CommandLineArgumentFunction("info", "coop.debug.town")]
    public static string Info(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.town.info <townId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[0], out Town town) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        Fief fief = town.Settlement.SettlementComponent as Fief;

        StringBuilder sb = new();

        sb.AppendFormat("ID: '{0}'\n", args[0]);
        sb.AppendFormat("Name: '{0}'\n", town.Name);
        sb.AppendFormat("Governor: '{0}'\n", (town.Governor != null) ? town.Governor.Name : "null");
        sb.AppendFormat("LastCapturedBy: '{0}'\n", (town.LastCapturedBy != null) ? town.LastCapturedBy.Name : "null");
        sb.AppendFormat("Prosperity: '{0}'\n", town.Prosperity);
        sb.AppendFormat("Loyalty: '{0}'\n", town.Loyalty);
        sb.AppendFormat("Security: '{0}'\n", town.Security);
        sb.AppendFormat("InRebelliousState: '{0}'\n", town.InRebelliousState);
        sb.AppendFormat("GarrisonAutoRecruitmentIsEnabled: '{0}'\n", town.GarrisonAutoRecruitmentIsEnabled);
        sb.AppendFormat("Food stock '{0}' : \n", fief.FoodStocks);
        sb.AppendFormat("TradeTaxAccumulated: '{0}'\n", town.TradeTaxAccumulated);
        sb.AppendFormat("Sold Items: \n");
        Town.SellLog[] logList = getSoldItems(town);
        if (logList != null)
        {
            foreach (Town.SellLog log in logList)
            {
                sb.AppendFormat("SellLog: {0} {1}\n", log.Category.StringId, log.Number);
            }
        }
        return sb.ToString();
    }

    // coop.debug.town.set_foodStocks
    /// <summary>
    /// Set the food stocks for a Town
    /// </summary>
    /// <param name="args">first arg : townId ; second arg : stock value</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("set_foodStocks", "coop.debug.town")]
    public static string SetFoodStocks(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_foodStocks <townId> <foodStocks> ";
        }

        string townId = args[0];
        string foodStocksString = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"Town with ID: '{townId}' not found";
        }
        
        Fief fief = town.Settlement.SettlementComponent as Fief;

        if (float.TryParse(foodStocksString, out float foodStocks) == false)
        {
            return $"Argument2: {foodStocksString} is not a float.";
        }

        fief.FoodStocks = foodStocks;

        return $"Town food stocks has changed to: {fief.FoodStocks}";
    }

    // coop.debug.town.set_governor town_comp_V1 lord_1_1
    /// <summary>
    /// Sets the Town governor of a specific Town.
    /// </summary>
    /// <param name="args">townID and the heroID to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_governor", "coop.debug.town")]
    public static string SetTownGovernor(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_governor <townId> <heroId> ";
        }

        string townId = args[0];
        string heroId = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"Town with ID: '{townId}' not found";
        }

        if (objectManager.TryGetObject(heroId, out Hero hero) == false)
        {
            return $"Hero with ID: '{heroId}' not found";
        }

        town.Governor = hero;

        return $"Town governor has changed to: {town.Governor?.Name} hero with ID: {town.Governor?.StringId}";
    }

    // coop.debug.town.set_last_captured_by town_comp_V1 clan_sturgia_2
    /// <summary>
    /// Sets the Town LastCapturedBy property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the clanID to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_last_captured_by", "coop.debug.town")]
    public static string SetTownLastCapturedBy(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_last_captured_by <townId> <clanId> ";
        }

        string townId = args[0];
        string clanId = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (objectManager.TryGetObject(clanId, out Clan clan) == false)
        {
            return $"{nameof(Clan)} with ID: '{clanId}' not found";
        }

        town.LastCapturedBy = clan;

        return $"{nameof(Town.LastCapturedBy)} has changed to: {town.LastCapturedBy.Name} clan with ID: {town.LastCapturedBy.StringId}";
    }

    // coop.debug.town.add_item_to_sold_items town_comp_V1 noble_horse 100
    /// <summary>
    /// Adds a number of items to the Town sold items list of a specific Town.
    /// </summary>
    /// <param name="args">townID and the itemID to add and a number to add.</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("add_item_to_sold_items", "coop.debug.town")]
    public static string AddToTownSoldItems(List<string> args)
    {
        if (args.Count != 3)
        {
            return "Usage: coop.debug.town.add_item_to_sold_items <townId> <itemId> <numberOfItems>";
        }

        string townId = args[0];
        string itemId = args[1];
        string count = args[2];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (objectManager.TryGetObject(itemId, out ItemCategory item) == false)
        {
            return $"{nameof(ItemCategory)} with ID: '{itemId}' not found";
        }

        if (int.TryParse(count, out int numberOfItems) == false)
        {
            return $"Argument3: {count} is not an integer.";
        }


        List<Town.SellLog> newSoldItems = new List<Town.SellLog>(getSoldItems(town));
        int idx = newSoldItems.FindIndex(log => log.Category == item);
        if (idx != -1)
        {
            newSoldItems[idx] = new Town.SellLog(item, newSoldItems[idx].Number + numberOfItems);
        }
        else
        {
            newSoldItems.Add(new Town.SellLog(item, numberOfItems));
        }
        town.SetSoldItems(newSoldItems);

        // Check if item was added
        if (town.SoldItems.Count(soldItem => soldItem.Category == item) <= 0)
        {
            return $"Unable to find {item} in {nameof(Town.SoldItems)}";
        }

        var newItem = town.SoldItems.First(soldItem => soldItem.Category == item);

        return $"Added {newItem.Number} number of {newItem.Category.StringId} to Town SoldItems";
    }

    // coop.debug.town.set_prosperity town_comp_V1 100
    /// <summary>
    /// Sets the Town prosperity of a specific Town.
    /// </summary>
    /// <param name="args">townID and the prosperity to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_prosperity", "coop.debug.town")]
    public static string SetTownProsperity(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_prosperity <townId> <prosperity> ";
        }

        string townId = args[0];
        string prosperityValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (int.TryParse(prosperityValue, out int prosperity) == false)
        {
            return $"Argument2: {prosperityValue} is not an integer.";
        }

        town.Prosperity = prosperity;
        return $"Town Prosperity has changed to: {town.Prosperity}.";
    }

    // coop.debug.town.set_loyalty town_comp_V1 100
    /// <summary>
    /// Sets the Town loyalty of a specific Town.
    /// </summary>
    /// <param name="args">townID and the loyalty to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_loyalty", "coop.debug.town")]
    public static string SetTownLoyalty(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_loyalty <townId> <loyalty> ";
        }

        string townId = args[0];
        string loyaltyValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (float.TryParse(loyaltyValue, out float loyalty) == false)
        {
            return $"Argument2: {loyaltyValue} is not a float.";
        }

        town.Loyalty = loyalty;
        return $"Town Loyalty has changed to: {town.Loyalty}.";
    }

    // coop.debug.town.set_security town_comp_V1 100
    /// <summary>
    /// Sets the Town security of a specific Town.
    /// </summary>
    /// <param name="args">townID and the security to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_security", "coop.debug.town")]
    public static string SetTownSecurity(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_loyalty <townId> <security> ";
        }

        string townId = args[0];
        string securityValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (float.TryParse(securityValue, out float security) == false)
        {
            return $"Argument2: {securityValue} is not a float.";
        }

        town.Security = security;
        return $"Town Security has changed to: {town.Security}.";
    }


    // coop.debug.town.set_in_rebellious_state town_comp_V1 true
    /// <summary>
    /// Sets the Town rebellious state of a specific Town.
    /// </summary>
    /// <param name="args">townID and the rebellious state to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_in_rebellious_state", "coop.debug.town")]
    public static string SetTownInRebelliousState(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_in_rebellious_state <townId> <in_rebellious_state> ";
        }

        string townId = args[0];
        string rebellionStateValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (bool.TryParse(rebellionStateValue, out bool inRebelliousState) == false)
        {
            return $"Argument2: {rebellionStateValue} is not a boolean.";
        }

        RebellionsCampaignBehaviorPatches.PublishTownInRebelliousStateChanged(town, inRebelliousState);
        return $"Town InRebelliousState has changed to: {town.InRebelliousState}.";
    }

    // coop.debug.town.set_garrison_auto_recruitment town_comp_V1 false
    /// <summary>
    /// Sets the Town GarrisonAutoRecruitmentIsEnabled property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the GarrisonAutoRecruitmentIsEnabled property value to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_garrison_auto_recruitment", "coop.debug.town")]
    public static string SetTownGarrisonAutoRecruitmentIsEnabled(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_garrison_auto_recruitment <townId> <garrison_auto_recruitment_enabled> ";
        }

        string townId = args[0];
        string garrisonRecruitmentValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (bool.TryParse(garrisonRecruitmentValue, out bool garrisonAutoRecruitmentIsEnabled) == false)
        {
            return $"Argument2: {garrisonRecruitmentValue} is not a boolean.";
        }

        UpdateClanSettlementAutoRecruitmentPatches.PublishTownGarrisonAutoRecruitmentIsEnabledChanged(town, garrisonAutoRecruitmentIsEnabled);
        return $"Town GarrisonAutoRecruitmentIsEnabled has changed to: {town.GarrisonAutoRecruitmentIsEnabled}.";
    }

    // coop.debug.town.set_trade_tax_acc town_comp_V1 100
    /// <summary>
    /// sets the tradetaxaccumulated value for a town.
    /// </summary>
    /// <param name="args">the town and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    [CommandLineArgumentFunction("set_trade_tax_acc", "coop.debug.town")]
    public static string SetTradeTaxAccumulated(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.town.set_trade_tax_acc <townId> <0.0> ";
        }

        string townId = args[0];
        string tradeTaxAccumulatedValue = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(townId, out Town town) == false)
        {
            return $"{nameof(Town)} with ID: '{townId}' not found";
        }

        if (int.TryParse(tradeTaxAccumulatedValue, out int tradeTaxAccumulated) == false)
        {
            return $"Argument2: {tradeTaxAccumulatedValue} is not an integer.";
        }

        town.TradeTaxAccumulated = tradeTaxAccumulated;
        return $"Town TradeTaxAccumulated has changed to: {town.TradeTaxAccumulated}.";
    }
}
