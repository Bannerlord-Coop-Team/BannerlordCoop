using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.ItemRosters.Commands
{
    internal class ItemRosterDebugCommands
    {
        [CommandLineArgumentFunction("Info", "Coop.Debug.ItemRosters")]
        public static string ItemRosterInfo(List<string> args)
        {
            if (args.Count < 1)
            {
                return "ID expected";
            }

            ItemRoster roster;

            if (MBObjectManager.Instance.ContainsObject<Settlement>(args[0]))
            {
                var obj = MBObjectManager.Instance.GetObject<Settlement>(args[0]);
                roster = obj.ItemRoster;

            } else if (MBObjectManager.Instance.ContainsObject<MobileParty>(args[0]))
            {
                var obj = MBObjectManager.Instance.GetObject<MobileParty>(args[0]);
                roster = obj.ItemRoster;
            } else
            {
                return String.Format("ID: '{0}' not found", args[0]);
            }

            return String.Format("Item count: {0}\nItem roster hash: {1:X}\n", roster.Count, hash(roster));
        }

        private static int hash(ItemRoster roster)
        {
            int hash = 17;
            foreach (var item in roster)
            {
                hash = hash * 31 + item.GetHashCode();
            }

            return hash;
        }
    }
}
