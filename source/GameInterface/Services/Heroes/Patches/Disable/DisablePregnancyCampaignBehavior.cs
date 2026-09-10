using Common;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(PregnancyCampaignBehavior))]
internal class DisablePregnancyCampaignBehavior
{
    [HarmonyPatch(nameof(PregnancyCampaignBehavior.RegisterEvents))]
#if TESTER
    static bool Prefix() => ModInformation.IsServer;
#else
    static bool Prefix() => false;
#endif
}

#if TESTER
[HarmonyPatch(typeof(PregnancyCampaignBehavior))]
internal class PregnancyCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PregnancyCampaignBehavior.CheckOffspringToDeliver))]
    [HarmonyPrefix]
    public static bool CheckOffspringToDeliverPrefix(PregnancyCampaignBehavior __instance, PregnancyCampaignBehavior.Pregnancy pregnancy)
    {
        if (!ContainerProvider.TryResolve<IPregnancyCampaignBehaviorInterface>(out var pregnancyBehaviorInterface)) return false;

        pregnancyBehaviorInterface.CheckOffspringToDeliver(__instance, pregnancy);

        return false;
    }

    [HarmonyPatch(nameof(PregnancyCampaignBehavior.CheckAreNearby))]
    [HarmonyPrefix]
    public static bool CheckAreNearbyPrefix(PregnancyCampaignBehavior __instance, ref bool __result, Hero hero, Hero spouse)
    {
        if (!ContainerProvider.TryResolve<IPregnancyCampaignBehaviorInterface>(out var pregnancyBehaviorInterface)) return false;

        __result = pregnancyBehaviorInterface.CheckAreNearby(__instance, hero, spouse);

        return false;
    }
}
#endif
