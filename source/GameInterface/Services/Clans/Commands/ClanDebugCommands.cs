using Autofac;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
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
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", clan.StringId, clan.Name));
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
    }
}
