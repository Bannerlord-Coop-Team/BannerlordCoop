using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    internal static class CampaignObjectManagerExtentions
    {
        public static List<MobileParty> GetPlayerMobileParties(this CampaignObjectManager campaignObjectManager)
        {
            var parties = new List<MobileParty>();
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return parties;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return parties;

            foreach (var player in playerManager.Players)
            {
                if (objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                {
                    parties.Add(party);
                }
            }

            return parties;
        }

        public static List<Clan> GetPlayerClans(this CampaignObjectManager campaignObjectManager)
        {
            var clans = new List<Clan>();
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return clans;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return clans;

            foreach (var player in playerManager.Players)
            {
                // Players can share a clan, so only add each one once
                if (objectManager.TryGetObject<Clan>(player.OriginalClanId ?? player.ClanId, out var clan) && !clans.Contains(clan))
                {
                    clans.Add(clan);
                }

                // Matches PlayerManager.TryGetControlledObjectInfo, which also counts clans led by a player hero
                if (objectManager.TryGetObject<Hero>(player.HeroId, out var hero)
                    && hero.Clan != null
                    && hero.Clan.Leader == hero
                    && !clans.Contains(hero.Clan))
                {
                    clans.Add(hero.Clan);
                }
            }

            return clans;
        }

        public static List<Hero> GetPlayerHeroes(this CampaignObjectManager campaignObjectManager)
        {
            var heroes = new List<Hero>();
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return heroes;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return heroes;

            foreach (var player in playerManager.Players)
            {
                if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero)) continue;

                // Matches CampaignObjectManager._aliveHeroes, which excludes dead and disabled heroes
                if (hero.HeroState == Hero.CharacterStates.Dead || hero.HeroState == Hero.CharacterStates.Disabled) continue;

                heroes.Add(hero);
            }

            return heroes;
        }
    }
}
