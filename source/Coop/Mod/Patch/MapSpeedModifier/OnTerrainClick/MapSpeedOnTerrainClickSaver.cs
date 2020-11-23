using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnTerrainClick
{

    /// <summary>
    ///     <para>
    ///         Code patcher through Harmony which saves current campaign map time speed on left mouse button click.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see>
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes private method <c>MapScreen::HandleLeftMouseButtonClick</c> to save 
    ///     current <c>TimeControlMode</c>.
    /// </remarks>
    // DISABLED
    //[HarmonyPatch(typeof(MapScreen))]
    //[HarmonyPatch("HandleLeftMouseButtonClick")]
    class MapSpeedOnTerrainClickSaver
    {

        public static CampaignTimeControlMode currentCampaignTimeControlMode;

        static void Prefix()
        {
            currentCampaignTimeControlMode = Campaign.Current.TimeControlMode;
        }

    }

}
