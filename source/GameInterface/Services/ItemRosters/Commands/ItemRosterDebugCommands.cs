using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;
using System.Collections.Immutable;
using TaleWorlds.Core;

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

            ItemRoster roster = null;
            string owner = null;

            if (MBObjectManager.Instance.ContainsObject<Settlement>(args[0]))
            {
                var obj = MBObjectManager.Instance.GetObject<Settlement>(args[0]);
                roster = obj.ItemRoster;

                owner = obj.Town.Name.ToString();

            }

            MobileParty party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
            if (party != null)
            {
                roster = party.ItemRoster;
                owner = party.Owner.Name.ToString();
            }
            
            if (roster == null || owner == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            return string.Format("ItemRoster info for '{0}':\n  Item count: {1}\n  Hash: {2:X}\n  Version No.: {3:X}\n",
                owner, roster.Count, hash(roster), roster.VersionNo);
        }

        private static int hash(ItemRoster roster)
        {
            int hash = 1009;
            var sorted = roster.ToImmutableSortedSet(new ItemRosterElementComparer());
            foreach (var item in sorted)
            {
                hash = hash * 9176 + item.EquipmentElement.Item.StringId.GetHashCode();
                if (item.EquipmentElement.ItemModifier != null)
                    hash = hash * 9176 + item.EquipmentElement.ItemModifier.StringId.GetHashCode();
                else
                    hash = hash * 9176 + 0;
                hash = hash * 9176 + item.Amount;
            }

            return hash;
        }

        private class ItemRosterElementComparer : IComparer<ItemRosterElement>
        {
            public int Compare(ItemRosterElement x, ItemRosterElement y)
            {
                return x.EquipmentElement.Item.StringId.CompareTo(y.EquipmentElement.Item.StringId);
            }
        }
    }
}
