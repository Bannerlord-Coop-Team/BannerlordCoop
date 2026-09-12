using Common;
using GameInterface.Services.Banners.Interfaces;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using System;
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
    // Used to only allow OnNewGameCreated to run GiveBannersToHeroes.
    // OnGameLoadFinishedEvent runs before players are registered, giving player clan heroes banners when loading a save.
    // DailyTickHero already eventually gives heroes with invalid banners new ones, so this event isn't really needed.
    [ThreadStatic]
    public static bool IsCreatingNewGame;

    [HarmonyPatch(nameof(BannerCampaignBehavior.OnNewGameCreated))]
    [HarmonyPrefix]
    public static void OnNewGameCreatedPrefix() => IsCreatingNewGame = true;

    [HarmonyPatch(nameof(BannerCampaignBehavior.OnNewGameCreated))]
    [HarmonyPostfix]
    public static void OnNewGameCreatedPostfix() => IsCreatingNewGame = false;

    [HarmonyPatch(nameof(BannerCampaignBehavior.GiveBannersToHeroes))]
    [HarmonyPrefix]
    public static bool GiveBannersToHeroesPrefix() => IsCreatingNewGame;

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