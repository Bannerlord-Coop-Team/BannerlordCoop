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

namespace GameInterface.Services.Villages.Commands
{
    internal class VillageDebugCommand
    {
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

            List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsVillage).ToList();

            Village village = settlements.Find(e => e.Village.StringId == args[0]).Village;

            if (village == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            return String.Format("ID: '{0}'\nVillageName: '{1}'\nVillageState: '{2}'\nVillageOwner: '{3}'\n", 
                args[0],village.Name, village.VillageState.ToString(), village.Owner.Name);

        }

        // coop.debug.village.set_state castle_village_comp_K7_2 BeingRaided
        /// <summary>
        /// Sets the VillageState of a specific Village.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>information if it changed</returns>
        [CommandLineArgumentFunction("set_state", "coop.debug.village")]
        public static string SetVillageState(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.village.set_state <villageId> <BeingRaided | ForcedForVolunteers | ForcedForSupplies | Looted> ";
            }

            List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsVillage).ToList();

            Village village = settlements.Find(e => e.Village.StringId == args[0]).Village;

            if (village == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }



            if(!Enum.TryParse(args[1], out Village.VillageStates villageState))
            {
                return string.Format("InvalidVillageState: '{0}' not found", args[0]);
            }
            village.VillageState = villageState;

            return String.Format("VillageState has changed to: {0}", villageState);
        }
    }
}
