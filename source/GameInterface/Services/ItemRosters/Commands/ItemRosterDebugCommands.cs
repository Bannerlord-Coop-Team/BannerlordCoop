using Common.Commands;
using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameInterface.Utils.Commands;
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

        // Adds one random item to a settlement's roster.
        public sealed class ItemRostersAddRandomItemCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ItemRostersAddRandomItemCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.item_rosters";

            public string Name => "add_random_item";

            public string Description => "Runs the add random item debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlement_id", "Settlement name or id; quote names with spaces.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (!resolver.TryResolveSettlement(args[0], out var settlement, out var error)) return Failed(error);

                Random random = new();

                var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

                var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

                settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);

                return Succeeded($"Added {randomItem.Name} to {settlement.Name}'s ItemRoster");
            }
        }

        // Adds the same random item to a settlement's roster many times in one tick.
        public sealed class ItemRostersAddItemBurstCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ItemRostersAddItemBurstCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.item_rosters";

            public string Name => "add_item_burst";

            public string Description => "Runs the add item burst debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Server;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlement_id", "Settlement name or id; quote names with spaces.", isRequired: true),
                new ExpectedArgs("count", "The positive number of items to add.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (ModInformation.IsClient)
                {
                    return Failed("Run this on the server; it is authoritative and replicates to clients.");
                }

                if (!resolver.TryResolveSettlement(args[0], out var settlement, out var error)) return Failed(error);

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

        // Reports the item count, hash and food of a settlement's or party's roster.
        public sealed class ItemRostersInfoCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ItemRostersInfoCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.item_rosters";

            public string Name => "info";

            public string Description => "Reports info.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("party_or_settlement_id", "Party or settlement name or id; quote names with spaces.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (!TryFindItemRoster(resolver, args[0], out var roster, out var owner, out var error)) return Failed(error);

                return Succeeded(string.Format("ItemRoster info for '{0}':\n  Items: {1}\n  Count: {2}\n  SHA1: {3:X}\n  Food: {4}\n",
                    owner, roster.Count, roster.Sum((i) => { return i.Amount; }), ItemRosterHash(roster),
                    roster.TotalFood));
            }
        }

        // Writes a settlement's or party's roster contents to a text file.
        public sealed class ItemRostersExportCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ItemRostersExportCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.item_rosters";

            public string Name => "export";

            public string Description => "Runs the export debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("party_or_settlement_id", "Party or settlement name or id; quote names with spaces.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (!TryFindItemRoster(resolver, args[0], out var roster, out var owner, out var error)) return Failed(error);

                var name = "!" + (ModInformation.IsServer ? "server-itemroster-export-" : "client-itemroster-export-") + $"{SanitizeFileName(owner)}.txt";
                File.WriteAllText(name, ItemRosterContent(roster));

                return Succeeded($"Exported '{owner}' into '{name}'.\n Check bannerlord bin directory.");
            }
        }

        // Resolves a settlement or mobile party by name or id and returns its roster, failing when neither or both match.
        private static bool TryFindItemRoster(ICommandObjectResolver resolver, string query, out ItemRoster roster, out string name, out string error)
        {
            roster = null;
            name = null;

            var isSettlement = resolver.TryResolveSettlement(query, out var settlement, out var settlementError);
            var isParty = resolver.TryResolveMobileParty(query, out var party, out var partyError);

            if (isSettlement && isParty)
            {
                error = $"'{query}' matches both a settlement and a party; use an id. Settlement: {settlement.Name} ({settlement.StringId}), party: {party.Name} ({party.StringId})";
                return false;
            }

            if (isSettlement)
            {
                roster = settlement.ItemRoster;
                name = settlement.Name.ToString();
                error = null;
                return true;
            }

            if (isParty)
            {
                roster = party.ItemRoster;
                name = party.Name.ToString();
                error = null;
                return true;
            }

            error = $"{settlementError} {partyError}";
            return false;
        }

        // Replaces characters that are invalid in file names, since owner names come from user-facing text.
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
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
