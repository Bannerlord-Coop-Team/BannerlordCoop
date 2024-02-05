using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands
{
    internal class ClanDebugCommands
    {
        [CommandLineArgumentFunction("mapevents", "coop.debug")]
        public static string ChangeClanLeader(List<string> strings)
        {
            List<MobileParty> parties = MobileParty.AllBanditParties;

            return "command ran";
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
