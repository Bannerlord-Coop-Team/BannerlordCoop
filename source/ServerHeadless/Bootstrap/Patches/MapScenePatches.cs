using HarmonyLib;
using SandBox;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The map scene is backed by a native scene that never exists headless. Every query is
    /// answered from the exported nav grid when one is loaded (<see cref="HeadlessNavGrid"/> —
    /// real faces, terrain types, islands and A* pathfinding), and falls back to the old
    /// placeholder stubs when it is not (terrain-blind movement, loudly reported at bootstrap).
    /// </summary>
    [HarmonyPatch(typeof(MapScene))]
    internal class MapScenePatches
    {
        private static HeadlessNavGrid Grid => HeadlessNavGrid.Instance;

        /// <summary>
        /// Diagnostic feature gate: NAVGRID_DISABLE=faces,paths,lines,points reverts the named
        /// query groups to the old terrain-blind stub answers while the rest stay grid-backed.
        /// Used to bisect which query group changes campaign AI behavior on fresh worlds.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> DisabledFeatures =
            new System.Collections.Generic.HashSet<string>(
                (System.Environment.GetEnvironmentVariable("NAVGRID_DISABLE") ?? string.Empty)
                    .ToLowerInvariant()
                    .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        private static bool FeatureOff(string feature) => DisabledFeatures.Contains(feature);

        // ---- faces and terrain -----------------------------------------------------------------

        [HarmonyPatch(nameof(MapScene.GetFaceIndex))]
        [HarmonyPrefix]
        static bool GetFaceIndexPrefix(ref CampaignVec2 vec2, ref PathFaceRecord __result)
        {
            __result = Grid == null || FeatureOff("faces")
                ? new PathFaceRecord()
                : Grid.FaceRecordAt(vec2.ToVec2());
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetFaceAtIndex))]
        [HarmonyPrefix]
        static bool GetFaceAtIndexPrefix(int faceIndex, ref PathFaceRecord __result)
        {
            if (FeatureOff("faces"))
            {
                __result = PathFaceRecord.NullFaceRecord;
                return false;
            }

            // Callers enumerate faces densely 0..N-1 (the export does the same), so the argument
            // doubles as the ordinal; fall back to a lookup by native face id.
            if (Grid != null && faceIndex >= 0 && faceIndex < Grid.Faces.Length)
            {
                __result = Grid.MakeRecord(Grid.Faces[faceIndex]);
            }
            else
            {
                __result = Grid != null && Grid.TryGetFaceByIndex(faceIndex, out var face)
                    ? Grid.MakeRecord(face)
                    : PathFaceRecord.NullFaceRecord;
            }
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetFaceTerrainType))]
        [HarmonyPrefix]
        static bool GetFaceTerrainTypePrefix(PathFaceRecord navMeshFace, ref TerrainType __result)
        {
            __result = Grid == null || FeatureOff("faces")
                ? TerrainType.Plain
                : Grid.TerrainOfFaceIndex(navMeshFace.FaceIndex);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetTerrainTypeAtPosition))]
        [HarmonyPrefix]
        static bool GetTerrainTypeAtPositionPrefix(ref CampaignVec2 position, ref TerrainType __result)
        {
            __result = Grid == null || FeatureOff("faces")
                ? TerrainType.Plain
                : Grid.TerrainAt(position.ToVec2());
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetNumberOfNavigationMeshFaces))]
        [HarmonyPrefix]
        static bool GetNumberOfNavigationMeshFacesPrefix(ref int __result)
        {
            __result = Grid == null || FeatureOff("faces") ? 0 : Grid.Faces.Length;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetNavigationMeshCenterPosition), typeof(int))]
        [HarmonyPrefix]
        static bool GetNavigationMeshCenterPositionByOrdinalPrefix(int faceIndex, ref Vec2 __result)
        {
            __result = Grid != null && !FeatureOff("faces") && faceIndex >= 0 && faceIndex < Grid.Faces.Length
                ? Grid.Faces[faceIndex].Center
                : Vec2.Zero;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetNavigationMeshCenterPosition), typeof(PathFaceRecord))]
        [HarmonyPrefix]
        static bool GetNavigationMeshCenterPositionByFacePrefix(PathFaceRecord face, ref Vec2 __result)
        {
            __result = Grid != null && !FeatureOff("faces") && Grid.TryGetFaceByIndex(face.FaceIndex, out var f)
                ? f.Center
                : Vec2.Zero;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetFaceVertexZ))]
        [HarmonyPrefix]
        static bool GetFaceVertexZPrefix(PathFaceRecord navMeshFace, ref float __result)
        {
            __result = Grid != null && !FeatureOff("faces") && Grid.TryGetFaceByIndex(navMeshFace.FaceIndex, out var f)
                ? f.Z
                : 0f;
            return false;
        }

        // ---- pathfinding -------------------------------------------------------------------------

        [HarmonyPatch(nameof(MapScene.GetPathBetweenAIFaces))]
        [HarmonyPrefix]
        static bool GetPathBetweenAIFacesPrefix(
            Vec2 startingPosition, Vec2 endingPosition, NavigationPath path, int[] excludedFaceIds,
            int regionSwitchCostFromLandToSea, int regionSwitchCostFromSeaToLand, ref bool __result)
        {
            __result = false;
            path.Size = 0;
            if (Grid == null || FeatureOff("paths")) return false;

            var points = new List<Vec2>();
            if (!Grid.TryFindPath(startingPosition, endingPosition, excludedFaceIds,
                    regionSwitchCostFromLandToSea, regionSwitchCostFromSeaToLand, points, out _,
                    maxWaypoints: path.PathPoints.Length - 1))
                return false;

            int count = System.Math.Min(points.Count, path.PathPoints.Length);
            for (int i = 0; i < count; i++)
            {
                path.PathPoints[i] = points[i];
            }
            path.Size = count;
            __result = count > 0;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetPathDistanceBetweenAIFaces))]
        [HarmonyPrefix]
        static bool GetPathDistanceBetweenAIFacesPrefix(
            Vec2 startingPosition, Vec2 endingPosition, float distanceLimit, ref float distance,
            int[] excludedFaceIds, int regionSwitchCostFromLandToSea, int regionSwitchCostFromSeaToLand,
            ref bool __result)
        {
            distance = 0f;
            __result = false;
            if (Grid == null || FeatureOff("paths")) return false;

            // Estimate, not A*: this is the AI's target-scoring query and runs at very high
            // volume (dozens of candidates per party per think). Exact A* stays reserved for
            // GetPathBetweenAIFaces, which only runs when a party actually moves.
            if (Grid.TryEstimatePathDistance(startingPosition, endingPosition, excludedFaceIds, out float estimate))
            {
                distance = estimate;
                __result = distanceLimit <= 0f || estimate <= distanceLimit;
            }
            return false;
        }

        [HarmonyPatch(nameof(MapScene.IsLineToPointClear))]
        [HarmonyPrefix]
        static bool IsLineToPointClearPrefix(PathFaceRecord startingFace, Vec2 position, Vec2 destination, ref bool __result)
        {
            // Movement uses this to decide "walk straight instead of pathfinding" — with no
            // terrain restriction, a line across the ocean reports clear and parties march
            // through water. Land parties must not cross sea faces; ships (sea starting face)
            // keep the unrestricted line.
            if (Grid == null || FeatureOff("lines"))
            {
                __result = false;
                return false;
            }

            int[] exclusions = Grid.IsSeaFace(startingFace.FaceIndex) ? null : HeadlessNavGrid.DefaultLandExclusions;
            __result = Grid.IsLineClear(position, destination, exclusions);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetLastPointOnNavigationMeshFromPositionToDestination))]
        [HarmonyPrefix]
        static bool GetLastPointOnNavMeshPrefix(PathFaceRecord startingFace, Vec2 position, Vec2 destination, int[] excludedFaceIds, ref Vec2 __result)
        {
            if (Grid == null || FeatureOff("lines"))
            {
                __result = position;
                return false;
            }

            int[] exclusions = excludedFaceIds
                ?? (Grid.IsSeaFace(startingFace.FaceIndex) ? null : HeadlessNavGrid.DefaultLandExclusions);
            __result = Grid.LastClearPointOnLine(position, destination, exclusions);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetLastPositionOnNavMeshFaceForPointAndDirection))]
        [HarmonyPrefix]
        static bool GetLastPositionOnNavMeshFacePrefix(PathFaceRecord startingFace, Vec2 position, Vec2 destination, ref Vec2 __result)
        {
            if (Grid == null || FeatureOff("lines"))
            {
                __result = position;
                return false;
            }

            int[] exclusions = Grid.IsSeaFace(startingFace.FaceIndex) ? null : HeadlessNavGrid.DefaultLandExclusions;
            __result = Grid.LastClearPointOnLine(position, destination, exclusions);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetAccessiblePointNearPosition))]
        [HarmonyPrefix]
        static bool GetAccessiblePointNearPositionPrefix(ref CampaignVec2 pos, float radius, ref CampaignVec2 __result)
        {
            Vec2 basePos = pos.ToVec2();
            if (Grid == null || FeatureOff("points"))
            {
                __result = new CampaignVec2(basePos, true);
                return false;
            }
            Vec2 found = basePos;
            Grid.TryGetNearestAllowedPoint(basePos, HeadlessNavGrid.DefaultLandExclusions, radius, out found);
            __result = new CampaignVec2(found, !Grid.IsWaterAt(found));
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetNearestFaceCenterForPosition))]
        [HarmonyPrefix]
        static bool GetNearestFaceCenterForPositionPrefix(ref CampaignVec2 position, int[] excludedFaceIds, ref CampaignVec2 __result)
        {
            Vec2 basePos = position.ToVec2();
            if (Grid == null || FeatureOff("points"))
            {
                __result = new CampaignVec2(basePos, true);
                return false;
            }
            Vec2 found = basePos;
            Grid.TryGetNearestAllowedPoint(
                basePos, excludedFaceIds ?? HeadlessNavGrid.DefaultLandExclusions, 0f, out found);
            __result = new CampaignVec2(found, !Grid.IsWaterAt(found));
            return false;
        }

        // ---- heights and normals (flat approximations from face data) ---------------------------

        [HarmonyPatch(nameof(MapScene.GetHeightAtPoint))]
        [HarmonyPrefix]
        static bool GetHeightAtPointPrefix(ref CampaignVec2 point, ref float height, ref bool __result)
        {
            int ordinal = Grid?.OrdinalAt(point.ToVec2()) ?? -1;
            height = ordinal >= 0 ? Grid.Faces[ordinal].Z : 0f;
            __result = ordinal >= 0;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetTerrainHeightAndNormal))]
        [HarmonyPrefix]
        static bool GetTerrainHeightAndNormalPrefix(Vec2 position, ref float height, ref Vec3 normal)
        {
            int ordinal = Grid?.OrdinalAt(position) ?? -1;
            height = ordinal >= 0 ? Grid.Faces[ordinal].Z : 0f;
            normal = new Vec3(0f, 0f, 1f);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetGroundNormal))]
        [HarmonyPrefix]
        static bool GetGroundNormalPrefix(ref Vec3 __result)
        {
            __result = new Vec3(0f, 0f, 1f);
            return false;
        }

        // ---- scene metadata ----------------------------------------------------------------------

        [HarmonyPatch(nameof(MapScene.GetSceneXmlCrc))]
        [HarmonyPrefix]
        static bool GetSceneXmlCrcPrefix(ref uint __result)
        {
            __result = Grid?.SceneXmlCrc ?? 0u;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetSceneNavigationMeshCrc))]
        [HarmonyPrefix]
        static bool GetSceneNavigationMeshCrcPrefix(ref uint __result)
        {
            __result = Grid?.NavMeshCrc ?? 0u;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetTerrainSize))]
        [HarmonyPrefix]
        static bool GetTerrainSizePrefix(ref Vec2 __result)
        {
            __result = Grid != null ? Grid.Max : new Vec2(2000f, 2000f);
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetMapBorders))]
        [HarmonyPrefix]
        static bool GetMapBordersPrefix(ref Vec2 minimumPosition, ref Vec2 maximumPosition, ref float maximumHeight)
        {
            if (Grid != null)
            {
                minimumPosition = Grid.Min;
                maximumPosition = Grid.Max;
                maximumHeight = Grid.MaxHeight;
            }
            else
            {
                minimumPosition = new Vec2(0f, 0f);
                maximumPosition = new Vec2(1000f, 1000f);
                maximumHeight = 100f;
            }
            return false;
        }

        [HarmonyPatch(nameof(MapScene.DisableUnwalkableNavigationMeshes))]
        [HarmonyPrefix]
        static bool DisableUnwalkableNavigationMeshesPrefix() => false;

        // ---- weather / cosmetics (unchanged stubs) -------------------------------------------------

        [HarmonyPatch(nameof(MapScene.GetSnowAmountAtPosition))]
        [HarmonyPrefix]
        static bool GetSnowAmountAtPositionPrefix(ref float __result)
        {
            __result = 0f;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetRainAmountAtPosition))]
        [HarmonyPrefix]
        static bool GetRainAmountAtPositionPrefix(ref float __result)
        {
            __result = 0f;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetSiegeCampFrames))]
        [HarmonyPrefix]
        static bool GetSiegeCampFramesPrefix(ref List<MatrixFrame> siegeCamp1GlobalFrames, ref List<MatrixFrame> siegeCamp2GlobalFrames)
        {
            siegeCamp1GlobalFrames = new List<MatrixFrame>();
            siegeCamp2GlobalFrames = new List<MatrixFrame>();
            return false;
        }
    }
}
