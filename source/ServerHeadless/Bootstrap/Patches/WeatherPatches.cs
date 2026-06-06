using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using static TaleWorlds.CampaignSystem.ComponentInterfaces.MapWeatherModel;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The weather model samples the native map scene (snow/rain/terrain) to derive weather. Headless
    /// there is no scene, so report clear weather everywhere and skip the scene-based computation.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMapWeatherModel))]
    internal class WeatherPatches
    {
        [HarmonyPatch(nameof(DefaultMapWeatherModel.UpdateWeatherForPosition))]
        [HarmonyPrefix]
        static bool UpdateWeatherForPositionPrefix(ref WeatherEvent __result)
        {
            __result = WeatherEvent.Clear;
            return false;
        }

        [HarmonyPatch(nameof(DefaultMapWeatherModel.GetWeatherEventInPosition))]
        [HarmonyPrefix]
        static bool GetWeatherEventInPositionPrefix(ref WeatherEvent __result)
        {
            __result = WeatherEvent.Clear;
            return false;
        }
    }

    /// <summary>
    /// The periodic weather update walks a node grid sized from the (placeholder) map, which is
    /// empty headless — its first index throws. Weather is cosmetic, so skip the update entirely.
    /// </summary>
    [HarmonyPatch(typeof(MapWeatherCampaignBehavior), "WeatherUpdateTick")]
    internal class MapWeatherBehaviorPatches
    {
        static bool Prefix() => false;
    }
}
