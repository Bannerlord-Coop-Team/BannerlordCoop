using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The map-distance model resolves settlement-to-settlement distances via a navigation cache
    /// built on the native map scene's navigation mesh, which doesn't exist headless. Approximate
    /// with the straight-line distance between settlement gate positions (land only). This is used
    /// both during load (CalculateAverageDistanceBetweenTowns) and throughout the simulation.
    /// </summary>
    [HarmonyPatch]
    internal class MapDistancePatches
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

        static bool Prefix(Settlement fromSettlement, Settlement toSettlement, ref float landRatio, ref float __result)
        {
            landRatio = 1f;
            __result = (fromSettlement != null && toSettlement != null)
                ? fromSettlement.GatePosition.Distance(toSettlement.GatePosition)
                : 0f;
            return false;
        }
    }

    /// <summary>
    /// Fortification neighbours are read from the (null, scene-backed) navigation cache. Report no
    /// neighbours headless — AI siege/target scoring that relies on this simply finds none.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMapDistanceModel), nameof(DefaultMapDistanceModel.GetNeighborsOfFortification))]
    internal class MapDistanceNeighborsPatch
    {
        static bool Prefix(ref MBReadOnlyList<Settlement> __result)
        {
            __result = new MBList<Settlement>();
            return false;
        }
    }

    /// <summary>
    /// Resolves the nearest settlement entrance for a navigation face via the (null) navigation
    /// cache. Report "none" headless — callers null-check the result.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMapDistanceModel), nameof(DefaultMapDistanceModel.GetClosestEntranceToFace))]
    internal class MapDistanceEntrancePatch
    {
        static bool Prefix(ref ValueTuple<Settlement, bool> __result)
        {
            __result = (null, false);
            return false;
        }
    }
}
