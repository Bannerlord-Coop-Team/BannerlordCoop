using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
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

            Random random = new();

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
                return "Usage: coop.debug.itemrosters.info <party base id> (i.e. town_V1)";
            }

            var roster = FindPartyBase(args[0], out string owner);
            
            if (roster == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            return string.Format("ItemRoster info for '{0}':\n  Items: {1}\n  Count: {2}\n  SHA1: {3:X}\n",
                owner, roster.Count, roster.Sum((i) => { return i.Amount; }), ItemRosterHash(roster));
        }

        [CommandLineArgumentFunction("export", "coop.debug.itemrosters")]
        public static string Export(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.export <party base id> (i.e. town_V1)";
            }

            var roster = FindPartyBase(args[0], out string owner);

            if (roster == null || owner == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            var name = "!" + (ModInformation.IsServer ? "server-itemroster-export-" : "client-itemroster-export-") + $"{owner}.txt";
            File.WriteAllText(name, ItemRosterContent(roster));

            return $"Exported '{owner}' into '{name}'.\n Check bannerlord bin directory.";
        }

        private static ItemRoster FindPartyBase(string id, out string name)
        {
            if (MBObjectManager.Instance.ContainsObject<Settlement>(id))
            {
                var obj = MBObjectManager.Instance.GetObject<Settlement>(id);
                

                name = obj.Town.Name.ToString();
                return obj.ItemRoster;
            }

            MobileParty party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(id);
            if (party != null)
            {
                
                name = party.Owner.Name.ToString();
                return party.ItemRoster;
            }

            name = null;
            return null;
        }

        private static string ItemRosterContent(ItemRoster roster)
        {
            StringBuilder content = new();

            var sorted = roster.ToList();
            sorted.Sort(new ItemRosterElementComparer());
            foreach (var item in sorted)
            {
                content.Append(item.EquipmentElement.Item.StringId + " ");
                if (item.EquipmentElement.ItemModifier != null)
                    content.Append(item.EquipmentElement.ItemModifier.StringId + " ");
                content.Append(item.Amount);
                content.AppendLine();
            }
            return content.ToString();
        }

        private static string ItemRosterHash(ItemRoster roster)
        {
            return HashString(ItemRosterContent(roster));
        }

        private static string HashString(string input)
        {
            using SHA1Managed sha1 = new();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        private class ItemRosterElementComparer : IComparer<ItemRosterElement>
        {
            public int Compare(ItemRosterElement x, ItemRosterElement y)
            {
                var first = x.EquipmentElement.Item.StringId;
                if (x.EquipmentElement.ItemModifier != null)
                    first += x.EquipmentElement.ItemModifier.StringId;
                first += x.Amount;

                var second = y.EquipmentElement.Item.StringId;
                if (y.EquipmentElement.ItemModifier != null)
                    second += y.EquipmentElement.ItemModifier.StringId;
                second += y.Amount;

                return first.CompareTo(second);
            }
        }
    }
}
