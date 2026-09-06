using Common.Commands;
using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.ItemRosters.Commands
{
    internal class ItemRosterDebugCommands
    {
        private static CoopCommandResult Succeeded(string output) =>
            new CoopCommandResult(true, output);

        private static CoopCommandResult Failed(string output) =>
            new CoopCommandResult(false, output, "command_failed");

        public sealed class ItemRostersAddRandomItemCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.item_rosters";

            public string Name => "add_random_item";

            public string Description => "Runs the add random item debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlement_id", "The settlement StringId.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                var settlementId = args[0];
                var settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementId);

                if (settlement == null) return Failed($"Unable to find settlement with id: {settlementId}");

                Random random = new();

                var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

                var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

                settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);

                return Succeeded($"Added {randomItem.Name} to {settlement.Name}'s ItemRoster");
            }
        }

        public sealed class ItemRostersAddItemBurstCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.item_rosters";

            public string Name => "add_item_burst";

            public string Description => "Runs the add item burst debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlement_id", "The settlement StringId.", isRequired: true),
                new ExpectedArgs("count", "The positive number of items to add.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (ModInformation.IsClient)
                {
                    return Failed("Run this on the server; it is authoritative and replicates to clients.");
                }


                var settlementId = args[0];
                var settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementId);

                if (settlement == null) return Failed($"Unable to find settlement with id: {settlementId}");

                if (!int.TryParse(args[1], out var count) || count < 1)
                {
                    return Failed($"Invalid count: '{args[1]}'. Provide a positive integer.");
                }

                var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

                if (itemEnumerable.Count == 0) return Failed("No items are loaded.");

                Random random = new();

                var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

                // Add the same item count times in one tick so the coalescer collapses them into a single
                // update carrying the final count.
                for (int i = 0; i < count; i++)
                {
                    settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);
                }

                return Succeeded($"Added {count}x {randomItem.Name} to {settlement.Name}'s ItemRoster in one tick");
            }
        }

        public sealed class ItemRostersInfoCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.item_rosters";

            public string Name => "info";

            public string Description => "Reports info.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("party_or_settlement_id", "The party or settlement StringId.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                var roster = FindItemRoster(args[0], out string owner);

                if (roster == null)
                {
                    return Failed(string.Format("ID: '{0}' not found", args[0]));
                }

                return Succeeded(string.Format("ItemRoster info for '{0}':\n  Items: {1}\n  Count: {2}\n  SHA1: {3:X}\n",
                    owner, roster.Count, roster.Sum((i) => { return i.Amount; }), ItemRosterHash(roster)));
            }
        }

        public sealed class ItemRostersExportCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.item_rosters";

            public string Name => "export";

            public string Description => "Runs the export debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("party_or_settlement_id", "The party or settlement StringId.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                var roster = FindItemRoster(args[0], out string owner);

                if (roster == null)
                {
                    return Failed(string.Format("ID: '{0}' not found", args[0]));
                }

                var name = "!" + (ModInformation.IsServer ? "server-itemroster-export-" : "client-itemroster-export-") + $"{owner}.txt";
                File.WriteAllText(name, ItemRosterContent(roster));

                return Succeeded($"Exported '{owner}' into '{name}'.\n Check bannerlord bin directory.");
            }
        }

        private static ItemRoster FindItemRoster(string id, out string name)
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
