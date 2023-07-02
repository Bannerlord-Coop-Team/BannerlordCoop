using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// Disables all functionality for Town
/// </summary>
[HarmonyPatch(typeof(Village))]
internal class VillagePatches
{
    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool VillageStatePrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(Village.Hearth), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool HearthPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(Village.TradeTaxAccumulated), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool TradeTaxAccumulatedPrefix()
    {
        return false;
    }
}
