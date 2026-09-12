using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(DefaultBanditDensityModel))]
internal class DefaultBanditDensityModelPatches
{
    [HarmonyPatch(nameof(DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan))]
    [HarmonyPostfix]
    public static void GetMaxSupportedNumberOfLootersForClanPostfix(DefaultBanditDensityModel __instance, ref int __result, Clan clan)
    {
        if (clan == __instance.DeserterClan)
        {
            // Use default if provided with a negative value
            var multiplier = 1f;
            if (ModConfigProvider.ModOptions.MaximumLootersMultiplier >= 0)
                multiplier = ModConfigProvider.ModOptions.MaximumLootersMultiplier;

            __result = (int)(__result * multiplier);
        }
    }
}
