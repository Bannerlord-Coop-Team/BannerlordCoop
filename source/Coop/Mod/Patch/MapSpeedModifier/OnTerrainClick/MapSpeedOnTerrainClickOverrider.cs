using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnTerrainClick
{

    /// <summary>
    ///     <para>
    ///         Code patcher through Harmony which patches campaign map time speed on terrain click.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see>
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes private method <c>MapScreen::OnTerrainClick</c> to replace current <c>TimeControlMode</c> 
    ///     as <c>MapSpeedResolver::Resolve</c> sees fit.
    /// </remarks>
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
