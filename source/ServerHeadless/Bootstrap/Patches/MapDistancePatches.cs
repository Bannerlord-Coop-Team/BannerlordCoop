using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Shared grid-backed distance estimate for every DefaultMapDistanceModel overload. The
    /// native model resolves distances via a navigation cache built on the map scene's navmesh,
    /// which doesn't exist headless — and the non-settlement overloads fall back to
    /// GetClosestEntranceToFace (stubbed to null headless), so unpatched they answer 1e8 /
    /// float.MaxValue for every query whose endpoints are not on the same nav face. Party AI
    /// scores every candidate target with these numbers; an all-unreachable answer makes Hold
    /// win every think, which froze parties the moment they stepped out of a settlement gate.
    /// </summary>
    internal static class HeadlessMapDistance
    {
        /// <summary>Farther than any real route on the map; arithmetic-safe unlike float.MaxValue.</summary>
        public const float Unreachable = 100_000f;

        /// <summary>
        /// Region-gated estimate: positions on land masses a land party cannot walk to (e.g.
        /// War Sails island ports — naval transitions do not exist headless) must read as
        /// effectively unreachable. A plain euclidean answer told trade/AI target selection
        /// that overseas towns were cheap, and caravans marched to the shore and piled up
        /// there forever. Same-landmass pairs keep the cheap winding-factor estimate.
        /// </summary>
        public static float Estimate(Vec2 from, Vec2 to)
        {
            var grid = HeadlessNavGrid.Instance;
            if (grid == null) return from.Distance(to);

            return grid.TryEstimatePathDistance(from, to, HeadlessNavGrid.DefaultLandExclusions, out float estimate)
                ? estimate
                : Unreachable;
        }
    }

    /// <summary>
    /// Settlement-to-settlement distances — used during load (CalculateAverageDistanceBetweenTowns)
    /// and by trade/AI target scoring throughout the simulation.
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
            if (fromSettlement == null || toSettlement == null)
            {
                __result = 0f;
                return false;
            }

            __result = HeadlessMapDistance.Estimate(
                fromSettlement.GatePosition.ToVec2(), toSettlement.GatePosition.ToVec2());
            return false;
        }
    }

    /// <summary>
    /// Party-to-settlement distances — the number AiVisitSettlementBehavior and friends score
    /// candidate targets with for any party that is NOT inside a settlement. Unpatched this is
    /// the overload that froze parties at settlement gates (see HeadlessMapDistance remarks).
    /// </summary>
    [HarmonyPatch]
    internal class MapDistancePartyToSettlementPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(
                typeof(DefaultMapDistanceModel),
                nameof(DefaultMapDistanceModel.GetDistance),
                new[]
                {
                    typeof(MobileParty), typeof(Settlement), typeof(bool),
                    typeof(MobileParty.NavigationType), typeof(float).MakeByRefType(),
                });

        static bool Prefix(MobileParty fromMobileParty, Settlement toSettlement, ref float estimatedLandRatio, ref float __result)
        {
            estimatedLandRatio = 1f;
            if (fromMobileParty == null || toSettlement == null)
            {
                __result = HeadlessMapDistance.Unreachable;
                return false;
            }

            __result = HeadlessMapDistance.Estimate(
                fromMobileParty.Position.ToVec2(), toSettlement.GatePosition.ToVec2());
            return false;
        }
    }

    /// <summary>
    /// Party-to-party distances (engage/flee/army scoring). The 4-parameter float overload
    /// delegates to this one virtually, so one patch covers both.
    /// </summary>
    [HarmonyPatch]
    internal class MapDistancePartyToPartyPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(
                typeof(DefaultMapDistanceModel),
                nameof(DefaultMapDistanceModel.GetDistance),
                new[]
                {
                    typeof(MobileParty), typeof(MobileParty), typeof(MobileParty.NavigationType),
                    typeof(float), typeof(float).MakeByRefType(), typeof(float).MakeByRefType(),
                });

        static bool Prefix(MobileParty fromMobileParty, MobileParty toMobileParty, float maxDistance,
            ref float distance, ref float landRatio, ref bool __result)
        {
            landRatio = 1f;
            if (fromMobileParty == null || toMobileParty == null)
            {
                distance = HeadlessMapDistance.Unreachable;
                __result = false;
                return false;
            }

            distance = HeadlessMapDistance.Estimate(
                fromMobileParty.Position.ToVec2(), toMobileParty.Position.ToVec2());
            __result = distance <= maxDistance;
            return false;
        }
    }

    /// <summary>Party-to-point distances (patrol/escort/point scoring).</summary>
    [HarmonyPatch]
    internal class MapDistancePartyToPointPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(
                typeof(DefaultMapDistanceModel),
                nameof(DefaultMapDistanceModel.GetDistance),
                new[]
                {
                    typeof(MobileParty), typeof(CampaignVec2).MakeByRefType(),
                    typeof(MobileParty.NavigationType), typeof(float).MakeByRefType(),
                });

        static bool Prefix(MobileParty fromMobileParty, ref CampaignVec2 toPoint, ref float landRatio, ref float __result)
        {
            landRatio = 1f;
            if (fromMobileParty == null)
            {
                __result = HeadlessMapDistance.Unreachable;
                return false;
            }

            __result = HeadlessMapDistance.Estimate(fromMobileParty.Position.ToVec2(), toPoint.ToVec2());
            return false;
        }
    }

    /// <summary>Settlement-to-point distances.</summary>
    [HarmonyPatch]
    internal class MapDistanceSettlementToPointPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(
                typeof(DefaultMapDistanceModel),
                nameof(DefaultMapDistanceModel.GetDistance),
                new[]
                {
                    typeof(Settlement), typeof(CampaignVec2).MakeByRefType(),
                    typeof(bool), typeof(MobileParty.NavigationType),
                });

        static bool Prefix(Settlement fromSettlement, ref CampaignVec2 toPoint, ref float __result)
        {
            if (fromSettlement == null)
            {
                __result = HeadlessMapDistance.Unreachable;
                return false;
            }

            __result = HeadlessMapDistance.Estimate(fromSettlement.GatePosition.ToVec2(), toPoint.ToVec2());
            return false;
        }
    }

    /// <summary>
    /// The one helper that reads GetClosestEntranceToFace directly instead of going through the
    /// model's GetDistance overloads. Unpatched it answers float.MaxValue with landRatio -1 for
    /// every pair headless (both entrance lookups are null), poisoning engage/military scoring
    /// arithmetic with infinities.
    /// </summary>
    [HarmonyPatch(typeof(Helpers.DistanceHelper), nameof(Helpers.DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty))]
    internal class DistanceHelperPartyToPartyPatch
    {
        static bool Prefix(MobileParty fromMobileParty, MobileParty toMobileParty, ref float landRatio, ref float __result)
        {
            landRatio = 1f;
            if (fromMobileParty == null || toMobileParty == null)
            {
                __result = HeadlessMapDistance.Unreachable;
                return false;
            }

            __result = HeadlessMapDistance.Estimate(
                fromMobileParty.Position.ToVec2(), toMobileParty.Position.ToVec2());
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
