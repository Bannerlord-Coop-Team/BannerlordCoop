using Common;
using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditSpawnCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(BanditSpawnCampaignBehavior))]
internal class BanditSpawnCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior.GetCurrentLimitForLooters))]
    [HarmonyPostfix]
    public static void GetCurrentLimitForLootersPostfix(ref int __result)
    {
        __result = (int)(__result * ModConfigProvider.ModOptions.MaximumLootersMultiplier);
    }

    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior._numberOfMaxBanditCountPerClanHideout), MethodType.Getter)]
    [HarmonyPostfix]
    public static void NumberOfMaxBanditCountPerClanHideoutGetterPostfix(ref int __result)
    {
        __result = (int)(__result * ModConfigProvider.ModOptions.MaximumLootersMultiplier);
    }
}
