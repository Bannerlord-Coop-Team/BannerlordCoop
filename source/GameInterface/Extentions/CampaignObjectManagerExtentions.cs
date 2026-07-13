using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    internal static class CampaignObjectManagerExtentions
    {
        public static List<MobileParty> GetPlayerMobileParties(this CampaignObjectManager campaignObjectManager)
        {
            return campaignObjectManager._mobileParties.Where(party => party.IsPlayerParty()).ToList();
        }

        public static List<Clan> GetPlayerClans(this CampaignObjectManager campaignObjectManager)
        {
            return campaignObjectManager._clans.Where(clan => clan.IsPlayerClan()).ToList();
        }

        public static List<Hero> GetPlayerHeroes(this CampaignObjectManager campaignObjectManager)
        {
            return campaignObjectManager._aliveHeroes.Where(hero => hero.IsPlayerHero()).ToList();
        }
    }
}
