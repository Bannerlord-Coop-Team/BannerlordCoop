using Common.Extensions;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Towns.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class TownDebugCommand
{
    private static readonly Func<Town, Town.SellLog[]> getSoldItems = typeof(Town).GetField("_soldItems", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Town, Town.SellLog[]>();

    /// <summary>
    /// Finds a specific town in game.
    /// </summary>
    /// <param name="townId">string id of the town to search</param>
    /// <returns>Town or null.</returns>
    public static Town findTown(string townId)
    {
        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown).ToList();
        Town town = settlements.Find(e => e.Town.StringId == townId)?.Town;
        return town;
    }

    /// <summary>
    /// Finds a specific item in game.
    /// </summary>
    /// <param name="itemId">string id of the item to search</param>
    /// <returns>ItemCategory or null.</returns>
    public static ItemCategory findItem(string itemId)
    {
        List<ItemCategory> items = Campaign.Current.ObjectManager.GetObjectsOfType<ItemCategory>().Select(obj => (ItemCategory)obj).ToList();
        ItemCategory item = items.Find(i => i.StringId == itemId);
        return item;
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

        List<ItemCategory> items = Campaign.Current.ObjectManager.GetObjectsOfType<ItemCategory>().Select(obj => (ItemCategory)obj).ToList();

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

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }


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

    /// <summary>
    /// Sets the Town governor of a specific Town.
    /// </summary>
    /// <param name="args">townID and the heroID to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_governor", "coop.debug.town")]
    public static string SetTownGovernor(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_governor <townId> <heroId> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        Hero hero = HeroDebugCommand.findHero(args[1]);

        if (hero == null)
        {
            return string.Format("Hero with ID: '{0}' not found", args[1]);
        }

        town.Governor = hero;

        return string.Format("Town governor has changed to: {0} hero with ID: {1}", hero.Name, hero.StringId);
    }

    /// <summary>
    /// Sets the Town LastCapturedBy property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the clanID to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_last_captured_by", "coop.debug.town")]
    public static string SetTownLastCapturedBy(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_last_captured_by <townId> <clanId> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        Clan clan = ClanDebugCommands.findClan(args[1]);

        if (clan == null)
        {
            return string.Format("Clan with ID: '{0}' not found", args[1]);
        }

        town.LastCapturedBy = clan;

        return string.Format("Town LastCapturedBy has changed to: {0} clan with ID: {1}", clan.Name, clan.StringId);
    }

    /// <summary>
    /// Adds a number of items to the Town sold items list of a specific Town.
    /// </summary>
    /// <param name="args">townID and the itemID to add and a number to add.</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("add_item_to_sold_items", "coop.debug.town")]
    public static string AddToTownSoldItems(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 3)
        {
            return "Usage: coop.debug.town.set_last_captured_by <townId> <itemId> <numberOfItems>";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        ItemCategory item = findItem(args[1]);

        if (item == null)
        {
            return string.Format("Item with ID: '{0}' not found", args[1]);
        }

        if (int.TryParse(args[2], out int numberOfItems))
        {
            List<Town.SellLog> newSoldItems = new List<Town.SellLog>(getSoldItems(town));
            int idx = newSoldItems.FindIndex(log => log.Category == item);
            if (idx != -1 && idx >= 0 && idx < newSoldItems.Count)
            {
                newSoldItems[idx] = new Town.SellLog(item, newSoldItems[idx].Number + numberOfItems);
            }
            else
            {
                newSoldItems.Add(new Town.SellLog(item, numberOfItems));
            }
            town.SetSoldItems(newSoldItems);
            return string.Format("Added {0} number of {1} to Town SoldItems", numberOfItems, item.StringId);
        }
        else
        {
            return string.Format("Argument3: {0} is not an integer.", args[2]);
        }
    }

    /// <summary>
    /// Sets the Town prosperity of a specific Town.
    /// </summary>
    /// <param name="args">townID and the prosperity to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_prosperity", "coop.debug.town")]
    public static string SetTownProsperity(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_prosperity <townId> <prosperity> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        if (int.TryParse(args[1], out int prosperity))
        {
            town.Prosperity = prosperity;
            return string.Format("Town Prosperity has changed to: {0}.", prosperity);
        }
        else
        {
            return string.Format("Argument2: {0} is not an integer.", args[1]);
        }
    }

    /// <summary>
    /// Sets the Town loyalty of a specific Town.
    /// </summary>
    /// <param name="args">townID and the loyalty to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_loyalty", "coop.debug.town")]
    public static string SetTownLoyalty(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_loyalty <townId> <loyalty> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        if (float.TryParse(args[1], out float loyalty))
        {
            town.Loyalty = loyalty;
            return string.Format("Town Loyalty has changed to: {0}.", loyalty);
        }
        else
        {
            return string.Format("Argument2: {0} is not a float.", args[1]);
        }
    }

    /// <summary>
    /// Sets the Town security of a specific Town.
    /// </summary>
    /// <param name="args">townID and the security to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_security", "coop.debug.town")]
    public static string SetTownSecurity(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_loyalty <townId> <security> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        if (float.TryParse(args[1], out float security))
        {
            town.Security = security;
            return string.Format("Town Security has changed to: {0}.", security);
        }
        else
        {
            return string.Format("Argument2: {0} is not a float.", args[1]);
        }
    }


    /// <summary>
    /// Sets the Town rebellious state of a specific Town.
    /// </summary>
    /// <param name="args">townID and the rebellious state to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_in_rebellious_state", "coop.debug.town")]
    public static string SetTownInRebelliousState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_in_rebellious_state <townId> <in_rebellious_state> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        if (bool.TryParse(args[1], out bool inRebelliousState))
        {
            town.InRebelliousState = inRebelliousState;
            TownPatches.PublishTownInRebelliousStateChanged(town, town.InRebelliousState);
            return string.Format("Town InRebelliousState has changed to: {0}.", inRebelliousState);
        }
        else
        {
            return string.Format("Argument2: {0} is not a boolean.", args[1]);
        }
    }

    /// <summary>
    /// Sets the Town GarrisonAutoRecruitmentIsEnabled property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the GarrisonAutoRecruitmentIsEnabled property value to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_garrison_auto_recruitment", "coop.debug.town")]
    public static string SetTownGarrisonAutoRecruitmentIsEnabled(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_garrison_auto_recruitment <townId> <garrison_auto_recruitment_enabled> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("Town with ID: '{0}' not found", args[0]);
        }

        if (bool.TryParse(args[1], out bool garrisonAutoRecruitmentIsEnabled))
        {
            town.GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
            TownPatches.PublishTownGarrisonAutoRecruitmentIsEnabledChanged(town, town.GarrisonAutoRecruitmentIsEnabled);
            return string.Format("Town GarrisonAutoRecruitmentIsEnabled has changed to: {0}.", garrisonAutoRecruitmentIsEnabled);
        }
        else
        {
            return string.Format("Argument2: {0} is not a boolean.", args[1]);
        }
    }

    /// <summary>
    /// sets the tradetaxaccumulated value for a town.
    /// </summary>
    /// <param name="args">the town and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    [CommandLineArgumentFunction("set_trade_tax_acc", "coop.debug.town")]
    public static string SetTradeTaxAccumulated(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.town.set_trade_tax_acc <townId> <0.0> ";
        }

        Town town = findTown(args[0]);

        if (town == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        if (int.TryParse(args[1], out int tradeTaxAccumulated))
        {
            town.TradeTaxAccumulated = tradeTaxAccumulated;
            return string.Format("Town TradeTaxAccumulated has changed to: {0}.", tradeTaxAccumulated);
        }
        else
        {
            return string.Format("Argument2: {0} is not an integer.", args[1]);
        }
    }
}
