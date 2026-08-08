using HarmonyLib;
using SandBox;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(MapScene))]
internal class MapScenePatches
{
    /// <summary>
    /// The headless harness has no map scene, so every face reports the same neutral terrain.
    /// This used to return a RANDOM <see cref="TerrainType"/>, which made every campaign model that
    /// branches on terrain nondeterministic — e.g. <c>DefaultMapVisibilityModel.GetPartySpottingRange</c>
    /// only consults <c>PartyBaseHelper.HasFeat</c> (the Battanian forest feat) on
    /// <see cref="TerrainType.Forest"/>, so roughly one party creation in twenty walked a code path the
    /// other nineteen skipped. Tests must not depend on a dice roll: keep this constant.
    /// </summary>
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
}
