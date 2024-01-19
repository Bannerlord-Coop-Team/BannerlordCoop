using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static TaleWorlds.Library.CommandLineFunctionality;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Settlements;
using System.Linq;

namespace GameInterface.Services.Villages.Commands;

internal class VillageDebugCommand
{
    private static Village findVillage(string villageId)
    {
        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsVillage).ToList();
        Village village = settlements.Find(e => e.Village.StringId == villageId)?.Village;
        return village;
    }
    // coop.debug.village.list
    /// <summary>
    /// Lists all the villages
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the villages</returns>
    [CommandLineArgumentFunction("list", "coop.debug.village")]
    public static string ListVillages(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsVillage).ToList();

        settlements.ForEach((settlement) =>
        {
            Village v = settlement.Village;
            stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", v.StringId, v.Name));
        });

        return stringBuilder.ToString();
    }

    /// coop.debug.village.info castle_village_comp_K7_2
    /// coop.debug.village.info village_ES1_3
    /// <summary>
    /// Gets information on a specific village
    /// </summary>
    /// <param name="args">vilage ID to lookup</param>
    /// <returns>Information regarding the village.</returns>
    [CommandLineArgumentFunction("info", "coop.debug.village")]
    public static string Info(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.village.info <villageId>";
        }

        Village village = findVillage(args[0]);

        if (village == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }


        StringBuilder sb = new();

        sb.AppendFormat("ID: '{0}'\n", args[0]);
        sb.AppendFormat("Name: '{0}'\n", village.Name);
        sb.AppendFormat("Owner: '{0}'\n", village.Owner.Name);
        sb.AppendFormat("State: '{0}'\n", village.VillageState.ToString());
        sb.AppendFormat("Hearth: '{0}'\n", village.Hearth);
        sb.AppendFormat("TradeTaxAccumulated: '{0}'\n", village.TradeTaxAccumulated);
        sb.AppendFormat("LastDemandStatisifiedTime: '{0}'\n", village.LastDemandSatisfiedTime);

        return sb.ToString();
    }

    // coop.debug.village.set_state castle_village_comp_K7_2 BeingRaided
    /// <summary>
    /// Sets the VillageState of a specific Village.
    /// </summary>
    /// <param name="args">villageID and the state to set</param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_state", "coop.debug.village")]
    public static string SetVillageState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.village.set_state <villageId> <BeingRaided | ForcedForVolunteers | ForcedForSupplies | Looted> ";
        }

        Village village = findVillage(args[0]);

        if (village == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }


        if(!Enum.TryParse(args[1], out Village.VillageStates villageState))
        {
            return string.Format("InvalidVillageState: '{0}' not found", args[0]);
        }
        village.VillageState = villageState;

        return string.Format("VillageState has changed to: {0}", villageState);
    }


    // coop.debug.village.set_hearth castle_village_comp_K7_2 2.0
    /// <summary>
    /// sets the hearth float value for a village.
    /// </summary>
    /// <param name="args">the village and hearth value float</param>
    /// <returns>string output if success</returns>
    [CommandLineArgumentFunction("set_hearth", "coop.debug.village")]
    public static string SetVillageHearth(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.village.set_state <villageId> <0.0> ";
        }

        Village village = findVillage(args[0]);

        if (village == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        float hearth = 0.0f;
        try
        {
            hearth = float.Parse(args[1]);
        }catch(Exception)
        {
            return string.Format("Failed to parse the value: {0}", hearth);
        }

        village.Hearth = hearth;

        return string.Format("Hearth has changed to to: {0}", hearth);
    }

    // coop.debug.village.set_trade_tax_acc castle_village_comp_K7_2 500
    /// <summary>
    /// sets the tradetaxaccumulated  value for a village.
    /// </summary>
    /// <param name="args">the village and hearth value float</param>
    /// <returns>string output if success</returns>
    [CommandLineArgumentFunction("set_trade_tax_acc", "coop.debug.village")]
    public static string SetTradeTaxAccumulated(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Usage: This command can only be used by the server for debugging purposes.";

        if (args.Count < 2)
        {
            return "Usage: coop.debug.village.set_state <villageId> <0.0> ";
        }

        Village village = findVillage(args[0]);

        if (village == null)
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        int tradeTaxAccumulated = 0;
        try
        {
            tradeTaxAccumulated = int.Parse(args[1]);
        }
        catch (Exception)
        {
            return string.Format("Failed to parse the value: {0}", tradeTaxAccumulated);
        }

        village.TradeTaxAccumulated = tradeTaxAccumulated;

        return string.Format("Hearth has changed to to: {0}", tradeTaxAccumulated);
    }
}
