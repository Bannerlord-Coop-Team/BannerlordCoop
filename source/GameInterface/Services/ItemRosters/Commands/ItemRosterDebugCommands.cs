using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using System.Collections.Immutable;
using TaleWorlds.Core;
using System.Linq;

using static TaleWorlds.Library.CommandLineFunctionality;
using System.Security.Cryptography;
using System;
using System.Text;
using System.IO;

namespace GameInterface.Services.ItemRosters.Commands
{
    internal class ItemRosterDebugCommands
    {
        [CommandLineArgumentFunction("add_random_item", "coop.debug.itemrosters")]
        public static string AddRandomItem(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.add_random_item <party base id> (i.e. town_V1)";
            }

            var settlementId = args[0];
            var settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementId);

            if (settlement == null) return $"Unable to find settlement with id: {settlementId}";

            Random random = new Random();

            var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

            var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

            settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);

            return $"Added {randomItem.Name} to {settlement.Name}'s ItemRoster";
        }

        [CommandLineArgumentFunction("info", "coop.debug.itemrosters")]
        public static string Info(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.info <party base id>";
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

            return string.Format("ItemRoster info for '{0}':\n  Items: {1}\n  Count: {2}\n  SHA1: {3:X}\n",
                owner, roster.Count, roster.Sum((i) => { return i.Amount; }), ItemRosterHash(roster));
        }

        private static string ItemRosterHash(ItemRoster roster)
        {
            StringBuilder content = new();
            var sorted = roster.ToImmutableSortedSet(new ItemRosterElementComparer());
            foreach (var item in sorted)
            {
                content.Append(item.EquipmentElement.Item.StringId + " ");
                if (item.EquipmentElement.ItemModifier != null)
                    content.Append(item.EquipmentElement.ItemModifier.StringId + " ");
                content.Append(item.Amount);
                content.AppendLine();
            }

            File.WriteAllText("." + (ModInformation.IsServer ? "server-itemroster-info.txt" : "client-itemroster-info.txt"), content.ToString());

            return HashString(content.ToString());
        }

        private static string HashString(string input)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2"));
                }

                return sb.ToString();
            }
        }

        private class ItemRosterElementComparer : IComparer<ItemRosterElement>
        {
            public int Compare(ItemRosterElement x, ItemRosterElement y)
            {
                int diff = 0;
                diff += x.EquipmentElement.Item.StringId.CompareTo(y.EquipmentElement.Item.StringId);
                if (x.EquipmentElement.ItemModifier != null)
                    diff += x.EquipmentElement.ItemModifier.StringId.CompareTo(y.EquipmentElement.ItemModifier?.StringId);

                diff += y.Amount - x.Amount;
                return diff;
            }
        }
    }
}
