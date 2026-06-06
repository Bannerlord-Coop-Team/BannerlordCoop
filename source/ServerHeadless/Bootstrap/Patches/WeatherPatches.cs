using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The weather model's per-node cache (DefaultMapWeatherModel._weatherDataCache) is normally
    /// allocated by SandBoxGameManager's loading step (which sets DefaultWeatherNodeDimension then
    /// calls MapWeatherModel.InitializeCaches). That step doesn't run in our headless load path, so
    /// the cache stays null and the first weather update NREs. Allocate it just before the weather
    /// behaviour's session-launched handler builds its node grid and primes every node — by then
    /// Campaign.Models is wired up and DefaultWeatherNodeDimension is set (see CampaignMapScenePatches).
    ///
    /// We intentionally do NOT stub the weather model itself: with the scene-backed snow/rain mocked
    /// to 0 (MapScenePatches) the weather is purely season/time driven, which is deterministic and
    /// lets the server maintain an evolving weather state to transfer to clients.
    /// </summary>
    [HarmonyPatch(typeof(MapWeatherCampaignBehavior), "OnSessionLaunchedEvent")]
    internal class WeatherCachePatches
    {
        static void Prefix()
        {
            Campaign.Current?.Models?.MapWeatherModel?.InitializeCaches();
        }
    }
}
