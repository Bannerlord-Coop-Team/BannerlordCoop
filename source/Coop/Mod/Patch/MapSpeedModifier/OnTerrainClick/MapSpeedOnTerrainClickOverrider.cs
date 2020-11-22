using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnTerrainClick
{
    [HarmonyPatch(typeof(MapScreen))]
    [HarmonyPatch("OnTerrainClick")]
    class MapSpeedOnTerrainClickOverrider
    {
        static void Prefix()
        {
            
            var currentTimeControlMode = MapSpeedOnTerrainClickSaver.currentCampaignTimeControlMode;

            Campaign.Current.TimeControlMode = MapSpeedResolver.Resolve(currentTimeControlMode, false);
            
        }

    }
}
