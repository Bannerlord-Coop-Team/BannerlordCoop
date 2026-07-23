using Common;
using GameInterface.Services.Banners.Interfaces;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BannerCampaignBehavior))]
internal class DisableBannerCampaignBehavior
{
    [HarmonyPatch(nameof(BannerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(BannerCampaignBehavior))]
internal class BannerCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BannerCampaignBehavior.DailyTickHero))]
    [HarmonyPrefix]
    public static bool DailyTickHeroPrefix(Hero hero)
    {
        // Block updating hero banners if part of a player clan
        return !hero.Clan.IsPlayerClan();
    }

    [HarmonyPatch(nameof(BannerCampaignBehavior.OnCollectLootItems))]
    [HarmonyPrefix]
    public static bool OnCollectLootItemsPrefix(BannerCampaignBehavior __instance, PartyBase winnerParty, ItemRoster gainedLoots)
    {
        ContainerProvider.TryResolve<IBannerCampaignBehaviorInterface>(out var bannerCampaignBehaviorInterface);

        bannerCampaignBehaviorInterface.OnCollectLootItems(__instance, winnerParty, gainedLoots);

        return false;
    }

    [HarmonyPatch(nameof(BannerCampaignBehavior.CanBannerBeGivenToHero))]
    [HarmonyPrefix]
    public static bool CanBannerBeGivenToHeroPrefix(ref bool __result, Hero hero)
    {
        // Override result if clan is a player clan
        if (hero.Clan.IsPlayerClan())
        {
            __result = false;
            return false;
        }

        return true;
    }
}