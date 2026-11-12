using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroDailyXpCampaignBehavior))]
internal class HeroDailyXpCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeroDailyXpCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroDailyXpCampaignBehavior.DailyTickHero))]
    [HarmonyPrefix]
    private static bool DailyTickHeroPrefix(Hero hero) => !hero.IsPlayerHero();
}
