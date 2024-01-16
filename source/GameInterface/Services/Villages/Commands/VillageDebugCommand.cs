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

namespace GameInterface.Services.Villages.Commands
{
    internal class VillageDebugCommand
    {

        [CommandLineArgumentFunction("info", "coop.debug.village")]
        public static string Info(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.village.info <villageId>";
            }

            Village village = Campaign.Current.CampaignObjectManager.Find<Village>(args[0]);

            if (village == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }
            return String.Format("ID: '{0}'\nVillageName: '{1}'\nVillageState: '{2}'\nVillageOwner: '{3}'\n", 
                args[0],village.Name, village.VillageState.ToString(), village.Owner.Name);

        }
        [CommandLineArgumentFunction("set_state", "coop.debug.village")]
        public static string SetVillageState(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.village.set_state <villageId> <state>";
            }

            Village village = Campaign.Current.CampaignObjectManager.Find<Village>(args[0]);

            if (village == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }


        }
    }
}
