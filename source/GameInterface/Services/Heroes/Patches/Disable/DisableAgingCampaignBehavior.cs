using Common;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(AgingCampaignBehavior))]
internal class DisableAgingCampaignBehavior
{
    [HarmonyPatch(nameof(AgingCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(AgingCampaignBehavior))]
internal class AgingCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(AgingCampaignBehavior.DailyTickHero))]
    [HarmonyPrefix]
    public static bool DailyTickHeroPrefix(AgingCampaignBehavior __instance, Hero hero)
    {
        if (!ContainerProvider.TryResolve<IAgingCampaignBehaviorInterface>(out var agingBehaviorInterface)) return false;

        agingBehaviorInterface.DailyTickHero(__instance, hero);

        return false;
    }

    [HarmonyPatch(nameof(AgingCampaignBehavior.OnHeroComesOfAge))]
    [HarmonyPrefix]
    public static bool OnHeroComesOfAgePrefix(AgingCampaignBehavior __instance, Hero hero)
    {
        if (!ContainerProvider.TryResolve<IAgingCampaignBehaviorInterface>(out var agingBehaviorInterface)) return false;

        agingBehaviorInterface.OnHeroComesOfAge(__instance, hero);

        return false;
    }
}