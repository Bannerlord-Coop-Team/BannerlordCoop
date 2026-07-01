using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan))]
    internal class ClanPatches
    {
        [HarmonyPatch(nameof(Clan.PlayerClan))]
        [HarmonyPatch(MethodType.Getter)]
        [HarmonyPrefix]
        static bool PlayerClanGetter()
        {
            if (Campaign.Current == null) return false;

            return true;
        }
        // Patch for server to use passed down ClientHero instead of server's MainHero
        // which leads to a different hero
        [HarmonyPatch(nameof(Clan.CreateCompanionToLordClan))]
        [HarmonyPrefix]
        public static bool CreateCompanionToLordClanPrefix(Hero hero, Settlement settlement, TextObject clanName, int newClanIconId, ref Clan __result)
        {
            Clan clan = Clan.CreateClan(ResolvedMainHeroContext.ResolvedMainHero.MapFaction.StringId + "_companion_clan");
            clan.ChangeClanName(clanName, clanName);
            clan.Culture = settlement.Culture;
            clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), newClanIconId);
            clan.Kingdom = ResolvedMainHeroContext.ResolvedMainHero.Clan.Kingdom;
            clan.Tier = Campaign.Current.Models.ClanTierModel.CompanionToLordClanStartingTier;
            clan.SetInitialHomeSettlement(settlement);
            hero.Clan = clan;
            clan.SetLeader(hero);
            clan.IsNoble = true;
            ChangeOwnerOfSettlementAction.ApplyByGift(settlement, hero);
            CampaignEventDispatcher.Instance.OnClanCreated(clan, true);
            __result = clan;
            return false;
        }
    }
}
