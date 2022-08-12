using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.Extentions
{
    internal static class CampaignObjectManagerExtentions
    {
        public static List<MobileParty> GetMobileParties(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_mobileParties", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<MobileParty>)field.GetValue(campaignObjectManager);
        }

        public static List<Hero> GetDeadOrDisabledHeros(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_deadOrDisabledHeroes", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<Hero>)field.GetValue(campaignObjectManager);
        }

        public static List<Hero> GetAliveHeros(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_aliveHeroes", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<Hero>)field.GetValue(campaignObjectManager);
        }

        public static List<Clan> GetClans(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_clans", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<Clan>)field.GetValue(campaignObjectManager);
        }

        public static List<Kingdom> GetKingdoms(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_kingdoms", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<Kingdom>)field.GetValue(campaignObjectManager);
        }

        /// <summary>
        /// Combination of Clan and Kingdoms.
        /// </summary>
        /// <param name="campaignObjectManager"></param>
        /// <returns></returns>
        public static List<IFaction> GetFactions(this CampaignObjectManager campaignObjectManager)
        {
            FieldInfo field = typeof(CampaignObjectManager).GetField("_factions", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<IFaction>)field.GetValue(campaignObjectManager);
        }
    }
}
