using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class TownDebugCommand
{

    /// <summary>
    /// Finds a specific town in game.
    /// </summary>
    /// <param name="townId">string id of the town to search</param>
    /// <returns>Village or null.</returns>
    public static Town findTown(string townId)
    {
        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown).ToList();
        Town town = settlements.Find(e => e.Town.StringId == townId)?.Town;
        return town;
    }

    // coop.debug.town.list
    /// <summary>
    /// Lists all the towns
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the towns</returns>
    [CommandLineArgumentFunction("list", "coop.debug.town")]
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
        sb.AppendFormat("Governor: '{0}'\n", town.Governor.Name);
        sb.AppendFormat("LastCapturedBy: '{0}'\n", town.LastCapturedBy.Name);
        sb.AppendFormat("Prosperity: '{0}'\n", town.Prosperity);
        sb.AppendFormat("Loyalty: '{0}'\n", town.Loyalty);
        sb.AppendFormat("Security: '{0}'\n", town.Security);
        sb.AppendFormat("InRebelliousState: '{0}'\n", town.InRebelliousState);
        sb.AppendFormat("GarrisonAutoRecruitmentIsEnabled: '{0}'\n", town.GarrisonAutoRecruitmentIsEnabled);
        sb.AppendFormat("TradeTaxAccumulated: '{0}'\n", town.TradeTaxAccumulated);

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
            return string.Format("Hero with ID: '{0}' not found", args[0]);
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
            return string.Format("Clan with ID: '{0}' not found", args[0]);
        }

        town.LastCapturedBy = clan;

        return string.Format("Town Prosperity has changed to: {0} clan with ID: {1}", clan.Name, clan.StringId);
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
