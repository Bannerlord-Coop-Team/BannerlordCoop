using HarmonyLib;
using SandBox;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(MapScene))]
internal class MapScenePatches
{
    [HarmonyPatch(nameof(MapScene.GetFaceTerrainType))]
    [HarmonyPrefix]
    static bool GetFaceTerrainTypePrefix(ref TerrainType __result)
    {
        Array values = Enum.GetValues(typeof(TerrainType));
        Random random = new Random();
        __result = (TerrainType)values.GetValue(random.Next(values.Length))!;

        return false;
    }

    [HarmonyPatch(nameof(MapScene.GetFaceIndex))]
    [HarmonyPrefix]
    static bool GetFaceIndexPrefix(ref PathFaceRecord __result)
    {
        __result = new PathFaceRecord();

        return false;
    }
}
