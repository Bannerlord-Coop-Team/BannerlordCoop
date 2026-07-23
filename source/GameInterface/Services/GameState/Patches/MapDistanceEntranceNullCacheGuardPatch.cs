using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Guards <see cref="DefaultMapDistanceModel.GetClosestEntranceToFace"/> against a null navigation cache.
/// </summary>
/// <remarks>
/// Same root cause as <see cref="MapDistanceNullCacheGuardPatch"/>: a joining client loads the transferred save
/// before the campaign-map scene's navigation mesh registers the distance cache. During that window this method
/// dereferences the still-null <c>_navigationCache</c> and throws an NRE that aborts the load. Report "none" while
/// the cache is missing (callers null-check the result); once it is registered the original path runs unchanged.
/// Mirrors the headless server's <c>MapDistanceEntrancePatch</c>, but only when the cache is absent so it never
/// affects normal queries.
/// </remarks>
[HarmonyPatch(typeof(DefaultMapDistanceModel), nameof(DefaultMapDistanceModel.GetClosestEntranceToFace))]
internal class MapDistanceEntranceNullCacheGuardPatch
{
    static bool Prefix(DefaultMapDistanceModel __instance, ref ValueTuple<Settlement, bool> __result)
    {
        // Cache is present: let the original resolve the accurate entrance.
        if (__instance._navigationCache != null) return true;

        __result = (null, false);
        return false;
    }
}
