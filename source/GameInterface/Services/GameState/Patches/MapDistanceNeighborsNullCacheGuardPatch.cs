using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Guards <see cref="DefaultMapDistanceModel.GetNeighborsOfFortification"/> against a null navigation cache.
/// </summary>
/// <remarks>
/// Same root cause as <see cref="MapDistanceNullCacheGuardPatch"/>: a joining client loads the transferred save
/// before the campaign-map scene's navigation mesh registers the distance cache. During that window, clan
/// home-settlement scoring (<c>DefaultSettlementValueModel.FindMostSuitableHomeSettlement</c> →
/// <c>SettlementHelper.GetNeighborScoreForConsideringClan</c>) calls this method, which dereferences the still-null
/// <c>_navigationCache</c> and throws an NRE that aborts the load. Report no neighbours while the cache is missing;
/// once it is registered the original (accurate) path runs unchanged. Mirrors the headless server's
/// <c>MapDistanceNeighborsPatch</c>, but only when the cache is absent so it never affects normal queries.
/// </remarks>
[HarmonyPatch(typeof(DefaultMapDistanceModel), nameof(DefaultMapDistanceModel.GetNeighborsOfFortification))]
internal class MapDistanceNeighborsNullCacheGuardPatch
{
    static bool Prefix(DefaultMapDistanceModel __instance, ref MBReadOnlyList<Settlement> __result)
    {
        // Cache is present: let the original compute the accurate, navigation-mesh-based neighbours.
        if (__instance._navigationCache != null) return true;

        __result = new MBList<Settlement>();
        return false;
    }
}
