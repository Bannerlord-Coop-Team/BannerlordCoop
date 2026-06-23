using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands
{
    public class ClanDebugCommands
    {
        /// <summary>
        /// Attempts to get the ObjectManager
        /// </summary>
        /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
        /// <returns>True if ObjectManager was resolved, otherwise False</returns>
        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }

        // coop.debug.clan.list
        /// <summary>
        /// Lists all the clans
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the clans</returns>
        [CommandLineArgumentFunction("list", "coop.debug.clan")]
        public static string ListClans(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Clan> clans = Campaign.Current.CampaignObjectManager.Clans.ToList();

            clans.ForEach((clan) =>
            {
                stringBuilder.AppendLine(string.Format("ID: '{0}' Name: '{1}'", clan.StringId, clan.Name));
            });

            return stringBuilder.ToString();
        }


        [CommandLineArgumentFunction("change_clan_leader", "coop.debug.clan")]
        public static string ChangeClanLeader(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.change_clan_leader <clanId> <heroId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string heroId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(heroId, out Hero newLeader))
            {
                return $"Argument2: Kingdom not found by ID: {heroId}";
            }

            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, newLeader);

            return clan.Name.ToString() + " has a new leader: " + newLeader.Name.ToString();
        }

        [CommandLineArgumentFunction("change_clan_kingdom", "coop.debug.clan")]
        public static string ChangeClanKingdom(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.change_clan_kingdom <clanId> <kingdomId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string kingdomId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(kingdomId, out Kingdom newKingdom))
            {
                return $"Argument2: Kingdom not found by ID: {kingdomId}";
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, newKingdom);

            return clan.Name.ToString() + " has join the kingdom : " + newKingdom.Name.ToString();
        }

        [CommandLineArgumentFunction("destroy_clan", "coop.debug.clan")]
        public static string DestroyClan(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.clan.destroy_clan <clanId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            DestroyClanAction.Apply(clan);

            return clan.Name.ToString() + " has been destroyed";
        }

        [CommandLineArgumentFunction("add_companion", "coop.debug.clan")]
        public static string AddCompanion(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.add_companion <clanId> <heroId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string heroId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(heroId, out Hero companion))
            {
                return $"Argument2: Hero not found by ID: {heroId}";
            }

            AddCompanionAction.Apply(clan, companion);

            return companion.Name.ToString() + " has joined " + clan.Name.ToString();
        }

        [CommandLineArgumentFunction("add_renown", "coop.debug.clan")]
        public static string AddRenown(List<string> args)
        {
            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.add_renown <clanId> <renown>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string renownStr = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!int.TryParse(renownStr, out int renown))
            {
                return $"Argument2: Renown {renownStr} is not a valid integer value.";
            }

            clan.AddRenown(renown);

            return clan.Name.ToString() + " given renown";
        }

        // coop.debug.clan.economy
        /// <summary>
        /// Read-only: prints a clan's battle-economy values (renown, influence, leader-party morale, and
        /// total troop xp). Run it on the host and on a client with the same clan id to compare the two.
        /// </summary>
        [CommandLineArgumentFunction("economy", "coop.debug.clan")]
        public static string ClanEconomy(List<string> args)
        {
            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            Clan clan;
            if (args.Count >= 1)
            {
                // The argument can be a StringId, or a display name (which may contain spaces, so rejoin them).
                string query = string.Join(" ", args);

                if (!objectManager.TryGetObject(query, out clan))
                {
                    clan = Campaign.Current?.CampaignObjectManager?.Clans
                        ?.FirstOrDefault(c => string.Equals(c.Name?.ToString(), query, System.StringComparison.OrdinalIgnoreCase));
                }

                if (clan == null)
                {
                    return $"Clan not found by id or name: '{query}'";
                }
            }
            else
            {
                // No argument: use this instance's main hero clan (works on a client). The host has no main
                // hero, so pass the clan id or name printed by a client's output.
                clan = Hero.MainHero?.Clan;
                if (clan == null)
                {
                    return "No main hero on this instance; pass a clan id or name: coop.debug.clan.economy <clanIdOrName>";
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Clan '{clan.Name}' ({clan.StringId})");
            stringBuilder.AppendLine($"  Renown:    {clan.Renown}");
            stringBuilder.AppendLine($"  Influence: {clan.Influence}");

            var leaderParty = clan.Leader?.PartyBelongedTo;
            if (leaderParty != null)
            {
                int totalTroopXp = 0;
                var roster = leaderParty.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    totalTroopXp += roster.GetElementXp(i);
                }

                stringBuilder.AppendLine($"  Leader party '{leaderParty.Name}':");
                stringBuilder.AppendLine($"    RecentEventsMorale: {leaderParty.RecentEventsMorale}");
                stringBuilder.AppendLine($"    Total troop xp:     {totalTroopXp}");
            }

            return stringBuilder.ToString();
        }
        // coop.debug.clan.join_kingdom Player12 empire
        [CommandLineArgumentFunction("join_kingdom", "coop.debug.clan")]
        public static string JoinKingdom(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 2)
                return "Usage: coop.debug.clan.join_kingdom <clanId> <kingdomId>";

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return $"Unable to get {nameof(IObjectManager)}";

            if (objectManager.TryGetObject<Clan>(args[0], out var clan) == false)
                return $"Unable to get Clan with {args[0]}";

            if (objectManager.TryGetObject<Kingdom>(args[1], out var kingdom) == false)
                return $"Unable to get Kingdom with {args[1]}";

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);

            return $"{clan.Name} joined {kingdom.Name}";
        }

        // coop.debug.clan.give_influence Player12 500
        [CommandLineArgumentFunction("give_influence", "coop.debug.clan")]
        public static string GiveInfluence(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 2)
                return "Usage: coop.debug.clan.give_influence <clanId> <amount>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            if (!float.TryParse(args[1], out float amount))
                return $"Unable to parse {args[1]} as float";

            ChangeClanInfluenceAction.Apply(clan, amount);

            return $"Gave {amount} influence to {clan.Name}";
        }
        // coop.debug.clan.info
        [CommandLineArgumentFunction("info", "coop.debug.clan")]
        public static string InfoClan(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: coop.debug.clan.info <clanId>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {clan.Name}");
            sb.AppendLine($"StringId: {clan.StringId}");
            sb.AppendLine($"Leader: {clan.Leader?.Name.ToString() ?? "none"}");
            sb.AppendLine($"Kingdom: {clan.Kingdom?.Name.ToString() ?? "none"}");
            sb.AppendLine($"Influence: {clan.Influence}");
            sb.AppendLine($"Renown: {clan.Renown}");
            sb.AppendLine($"Tier: {clan.Tier}");
            sb.AppendLine($"IsEliminated: {clan.IsEliminated}");
            sb.AppendLine($"Members: {string.Join(", ", clan.Heroes.Select(h => h.Name))}");
            return sb.ToString();
        }
    }
}
//coop.debug.clan.add_renown Player12 1000
// coop.debug.clan.join_kingdom Player12 empire
//coop.debug.clan.give_influence Player12 500