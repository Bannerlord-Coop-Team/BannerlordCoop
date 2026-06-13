using HarmonyLib;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Headless replacements for the campaign-map scene setup that normally requires a native scene.
    /// The empty class-level [HarmonyPatch] marks the class for PatchAll discovery; each method
    /// declares its own target.
    /// </summary>
    [HarmonyPatch]
    internal class CampaignMapScenePatches
    {
        /// <summary>
        /// <see cref="Campaign.LoadMapScene"/> creates and loads the native campaign-map scene
        /// (MapSceneCreator.CreateMapScene + MapScene.Load). Headless, install a bare
        /// <see cref="MapScene"/> wrapper (its query methods are mocked by <see cref="MapScenePatches"/>)
        /// and seed placeholder map bounds, skipping the native scene load.
        /// </summary>
        [HarmonyPatch(typeof(Campaign), "LoadMapScene")]
        [HarmonyPrefix]
        static bool LoadMapScenePrefix(Campaign __instance)
        {
            __instance._mapSceneWrapper = new MapScene();

            // Match the game's default weather-node grid (SandBoxGameManager uses 32). The weather
            // behaviour sizes its node grid + shuffled-index array as dimension^2 and the save's
            // _lastUpdatedNodeIndex assumes that size, so this must match to tick weather correctly.
            __instance.DefaultWeatherNodeDimension = 32;

            Campaign.MapMinimumPosition = new Vec2(0f, 0f);
            Campaign.MapMaximumPosition = new Vec2(1000f, 1000f);
            Campaign.MapMaximumHeight = 100f;
            Campaign.MapDiagonal = Campaign.MapMinimumPosition.Distance(Campaign.MapMaximumPosition);
            Campaign.MapDiagonalSquared = Campaign.MapDiagonal * Campaign.MapDiagonal;
            // The original multiplies by Models.MapDistanceModel.RegionSwitchCostFromLandToSea, but
            // Campaign.Models isn't wired up this early during load. Use a fixed placeholder.
            Campaign.PlayerRegionSwitchCostFromLandToSea = (int)(Campaign.MapDiagonal * 0.2f);
            Campaign.PathFindingMaxCostLimit =
                System.Math.Max(Campaign.PlayerRegionSwitchCostFromLandToSea * 100, (int)(Campaign.MapDiagonal * 500f));

            return false;
        }

        // The on-load map-change check recomputes cached map data from the native scene (settlement
        // positions, army positions, …). Headless there is no scene to diff against, so skip it.
        [HarmonyPatch(typeof(Campaign), "CheckMapUpdate")]
        [HarmonyPrefix]
        static bool CheckMapUpdatePrefix() => false;
    }
}
