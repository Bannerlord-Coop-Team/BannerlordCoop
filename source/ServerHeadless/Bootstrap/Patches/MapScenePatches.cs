using HarmonyLib;
using SandBox;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The map scene is backed by a native scene. Headless, return placeholder terrain/face data so
    /// campaign code that queries the map doesn't dereference a non-existent scene. Ported from the
    /// Coop test harness.
    /// </summary>
    [HarmonyPatch(typeof(MapScene))]
    internal class MapScenePatches
    {
        [HarmonyPatch(nameof(MapScene.GetFaceTerrainType))]
        [HarmonyPrefix]
        static bool GetFaceTerrainTypePrefix(ref TerrainType __result)
        {
            __result = TerrainType.Plain;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetFaceIndex))]
        [HarmonyPrefix]
        static bool GetFaceIndexPrefix(ref PathFaceRecord __result)
        {
            __result = new PathFaceRecord();
            return false;
        }

        // Scene CRCs are read off the native scene; return 0 headless.
        [HarmonyPatch(nameof(MapScene.GetSceneXmlCrc))]
        [HarmonyPrefix]
        static bool GetSceneXmlCrcPrefix(ref uint __result)
        {
            __result = 0u;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetSceneNavigationMeshCrc))]
        [HarmonyPrefix]
        static bool GetSceneNavigationMeshCrcPrefix(ref uint __result)
        {
            __result = 0u;
            return false;
        }

        // Pathfinding queries the native navigation mesh; report "no path" headless.
        [HarmonyPatch(nameof(MapScene.GetPathBetweenAIFaces))]
        [HarmonyPrefix]
        static bool GetPathBetweenAIFacesPrefix(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPatch(nameof(MapScene.GetPathDistanceBetweenAIFaces))]
        [HarmonyPrefix]
        static bool GetPathDistanceBetweenAIFacesPrefix(ref bool __result, ref float distance)
        {
            distance = 0f;
            __result = false;
            return false;
        }

        // Weather queries sample the native scene; report clear weather (no snow/rain) headless.
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

        // Siege-camp placement frames come from the native scene; no siege staging headless.
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
