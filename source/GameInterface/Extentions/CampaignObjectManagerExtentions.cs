using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
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

        private static readonly MethodInfo CampaignObjectManager_AddPartyToAppropriateList = typeof(CampaignObjectManager).GetMethod("AddPartyToAppropriateList", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CampaignObjectManager_mobileParties = typeof(CampaignObjectManager).GetField("_mobileParties", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddPartyToAppropriateList(this CampaignObjectManager campaignObjectManager, MobileParty party)
        {
            List<MobileParty> _mobileParties = (List<MobileParty>)CampaignObjectManager_mobileParties.GetValue(campaignObjectManager);
            if (_mobileParties.Contains(party)) return;

            CampaignObjectManager_AddPartyToAppropriateList.Invoke(campaignObjectManager, new object[] { party });
        }

        private static readonly FieldInfo CampaignObjectManager_deadOrDisabledHeroes = typeof(CampaignObjectManager).GetField("_deadOrDisabledHeroes", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CampaignObjectManager_aliveHeroes = typeof(CampaignObjectManager).GetField("_aliveHeroes", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddHeroToAppropriateList(this CampaignObjectManager campaignObjectManager, Hero hero)
        {
            if (hero.HeroState == Hero.CharacterStates.Dead || hero.HeroState == Hero.CharacterStates.Disabled)
            {
                List<Hero> _deadOrDisabledHeroes = (List<Hero>)CampaignObjectManager_deadOrDisabledHeroes.GetValue(campaignObjectManager);
                if (_deadOrDisabledHeroes.Contains(hero)) return;

                _deadOrDisabledHeroes.Add(hero);
            }
            else
            {
                List<Hero> _aliveHeroes = (List<Hero>)CampaignObjectManager_aliveHeroes.GetValue(campaignObjectManager);
                if (_aliveHeroes.Contains(hero)) return;

                _aliveHeroes.Add(hero);
            }
        }

        private static readonly FieldInfo CampaignObjectManager_clans = typeof(CampaignObjectManager).GetField("_clans", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CampaignObjectManager_factions = typeof(CampaignObjectManager).GetField("_factions", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddClanToAppropriateList(this CampaignObjectManager campaignObjectManager, Clan clan)
        {
            List<Clan> _clans = (List<Clan>)CampaignObjectManager_clans.GetValue(campaignObjectManager);
            if (_clans.Contains(clan) == false)
            {
                _clans.Add(clan);
            }

            List<IFaction> _factions = (List<IFaction>)CampaignObjectManager_factions.GetValue(campaignObjectManager);
            if (_factions.Contains(clan) == false)
            {
                _factions.Add(clan);
            }
        }

        private static readonly FieldInfo CampaignObjectManager_kingdoms = typeof(CampaignObjectManager).GetField("_kingdoms", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddKingdomToAppropriateList(this CampaignObjectManager campaignObjectManager, Kingdom kingdom)
        {
            List<Kingdom> _kingdoms = (List<Kingdom>)CampaignObjectManager_kingdoms.GetValue(campaignObjectManager);
            if (_kingdoms.Contains(kingdom) == false)
            {
                _kingdoms.Add(kingdom);
            }

            List<IFaction> _factions = (List<IFaction>)CampaignObjectManager_factions.GetValue(campaignObjectManager);
            if (_factions.Contains(kingdom) == false)
            {
                _factions.Add(kingdom);
            }
        }

        private static readonly MethodInfo CampaignObjectManager_AddMobileParty = typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddMobileParty(this CampaignObjectManager campaignObjectManager, MobileParty party)
        {
            CampaignObjectManager_AddMobileParty.Invoke(campaignObjectManager, new object[] { party });
        }

        private static readonly MethodInfo CampaignObjectManager_AddHero = typeof(CampaignObjectManager).GetMethod("AddHero", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddHero(this CampaignObjectManager campaignObjectManager, Hero hero)
        {
            CampaignObjectManager_AddHero.Invoke(campaignObjectManager, new object[] { hero });
        }

        private static readonly MethodInfo CampaignObjectManager_AddClan = typeof(CampaignObjectManager).GetMethod("AddClan", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddClan(this CampaignObjectManager campaignObjectManager, Clan clan)
        {
            CampaignObjectManager_AddClan.Invoke(campaignObjectManager, new object[] { clan });
        }

        private static readonly MethodInfo CampaignObjectManager_AddKingdom = typeof(CampaignObjectManager).GetMethod("AddKingdom", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void AddKingdom(this CampaignObjectManager campaignObjectManager, Kingdom kingdom)
        {
            CampaignObjectManager_AddKingdom.Invoke(campaignObjectManager, new object[] { kingdom });
        }
    }
}
