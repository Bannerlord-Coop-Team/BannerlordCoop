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


        [CommandLineArgumentFunction("change_clan_leader", "coop.debug")]
        public static string ChangeClanLeader(List<string> strings)
        {
            Clan clan = Clan.All[int.Parse(strings[0])];

            Hero newLeader = clan.Heroes[int.Parse(strings[1])];

            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, newLeader);

            return clan.Name.ToString() + " has a new leader: " + newLeader.Name.ToString();
        }

        [CommandLineArgumentFunction("change_clan_kingdom", "coop.debug")]
        public static string ChangeClanKingdom(List<string> strings)
        {
            Clan clan = Clan.All[int.Parse(strings[0])];

            Kingdom newKingdom = Kingdom.All[int.Parse(strings[1])];

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, newKingdom);

            return clan.Name.ToString() + " has join the kingdom : " + newKingdom.Name.ToString();
        }

        [CommandLineArgumentFunction("destroy_clan", "coop.debug")]
        public static string DestroyClan(List<string> strings)
        {
            Clan clan = Clan.All[int.Parse(strings[0])];

            DestroyClanAction.Apply(clan);

            return clan.Name.ToString() + " has been destroyed";
        }

        [CommandLineArgumentFunction("add_companion", "coop.debug")]
        public static string AddCompanion(List<string> strings)
        {
            Clan clan = Clan.All[int.Parse(strings[0])];

            Hero companion = Hero.AllAliveHeroes[int.Parse(strings[1])];

            AddCompanionAction.Apply(clan, companion);

            return companion.Name.ToString() + " has joined " + clan.Name.ToString();
        }

        [CommandLineArgumentFunction("add_renown", "coop.debug")]
        public static string AddRenown(List<string> strings)
        {
            Clan clan = Clan.All[int.Parse(strings[0])];

            clan.AddRenown(int.Parse(strings[1]));

            return clan.Name.ToString() + " given renown";
        }
    }
}
