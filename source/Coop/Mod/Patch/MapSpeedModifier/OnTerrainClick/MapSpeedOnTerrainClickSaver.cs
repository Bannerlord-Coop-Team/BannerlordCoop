using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnTerrainClick
{
    [HarmonyPatch(typeof(MapScreen))]
    [HarmonyPatch("HandleLeftMouseButtonClick")]
    class MapSpeedOnTerrainClickSaver
    {

        public static CampaignTimeControlMode currentCampaignTimeControlMode;

        static void Prefix()
        {
            currentCampaignTimeControlMode = Campaign.Current.TimeControlMode;
        }

    }
}
