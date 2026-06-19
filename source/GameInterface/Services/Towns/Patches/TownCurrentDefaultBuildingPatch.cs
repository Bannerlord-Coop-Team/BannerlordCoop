using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Patches;

// BuildingsCampaignBehavior is disabled on clients, so the daily-default flag isn't always
// maintained and Town.CurrentBuilding can be null where vanilla guarantees it isn't (crashing
// consumers like DefaultSettlementProsperityModel). Re-establish a fallback when it lapses; the
// real flag still arrives via field sync. Both getters are patched since CurrentBuilding inlines
// its call to CurrentDefaultBuilding.
[HarmonyPatch(typeof(Town))]
internal class TownCurrentDefaultBuildingPatch
{
    [HarmonyPatch(nameof(Town.CurrentDefaultBuilding), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CurrentDefaultBuildingPostfix(Town __instance, ref Building __result)
    {
        // Intentionally null while a building is in progress.
        if (__result != null || __instance.BuildingsInProgress.Count > 0) return;

        __result = FallbackBuilding(__instance);
    }

    [HarmonyPatch(nameof(Town.CurrentBuilding), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CurrentBuildingPostfix(Town __instance, ref Building __result)
    {
        if (__result != null) return;

        __result = FallbackBuilding(__instance);
    }

    private static Building FallbackBuilding(Town town) =>
        town.Buildings.FirstOrDefault(b => b.BuildingType.IsDailyProject)
        ?? town.Buildings.FirstOrDefault();
}
