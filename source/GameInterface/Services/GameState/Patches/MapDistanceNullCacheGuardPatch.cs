using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Guards <see cref="DefaultMapDistanceModel.GetDistance(Settlement, Settlement, bool, bool, MobileParty.NavigationType, out float)"/>
/// against a null navigation cache.
/// </summary>
/// <remarks>
/// The model's settlement-to-settlement distance reads a navigation cache that is registered (via
/// <see cref="DefaultMapDistanceModel.RegisterDistanceCache"/>) only when the campaign-map scene's navigation mesh
/// loads. A joining client loads the transferred save through <c>Campaign.DoLoadingForGameType</c>, whose
/// <c>CalculateCachedValues</c> step (<c>CalculateAverageDistanceBetweenTowns</c>) iterates every town pair and calls
/// this overload — which dereferences the cache with no null check — before that registration has happened, throwing
/// an NRE that aborts the load. Fall back to a straight-line gate-to-gate approximation while the cache is missing;
/// once it is registered the original (accurate) path runs unchanged. Mirrors the headless server's
/// <c>MapDistancePatches</c>, but only when the cache is absent so it never affects normal distance queries.
/// </remarks>
[HarmonyPatch]
internal class MapDistanceNullCacheGuardPatch
{
    static MethodBase TargetMethod()
        => AccessTools.Method(
            typeof(DefaultMapDistanceModel),
            nameof(DefaultMapDistanceModel.GetDistance),
            new[]
            {
                typeof(Settlement), typeof(Settlement), typeof(bool), typeof(bool),
                typeof(MobileParty.NavigationType), typeof(float).MakeByRefType(),
            });

    static bool Prefix(DefaultMapDistanceModel __instance, Settlement fromSettlement, Settlement toSettlement, ref float landRatio, ref float __result)
    {
        // Cache is present: let the original compute the accurate, navigation-mesh-based distance.
        if (__instance._navigationCache != null) return true;

        landRatio = 1f;
        __result = (fromSettlement != null && toSettlement != null && fromSettlement != toSettlement)
            ? fromSettlement.GatePosition.Distance(toSettlement.GatePosition)
            : 0f;
        return false;
    }
}
