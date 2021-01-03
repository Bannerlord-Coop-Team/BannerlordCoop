using HarmonyLib;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.CampaignPatches
{

    /// <summary>
    ///     <para>
    ///         Code patcher through Harmony which overrides new campaign map time speed on it's setter.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see>
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes setter <c>Campaign::TimeControlMode::set</c> to override it's new value 
    ///     as <c>MapSpeedResolver::Resolve</c> sees fit.
    /// </remarks>
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.TimeControlMode))]
    static class TimeControlModePatch
    {
        /// <summary>
        /// Grants access to the private field <c>TaleWorlds.CampaignSystem.Campaign._timeControlMode</c> to override the setter logic.
        /// </summary>
        static AccessTools.FieldRef<Campaign, CampaignTimeControlMode> _timeControlModeRef = AccessTools.FieldRefAccess<Campaign, CampaignTimeControlMode>("_timeControlMode");

        /// <summary>
        /// Overrides the <c>TaleWorlds.CampaignSystem.Campaign.TimeControlMode</c> setter logic
        /// </summary>
        /// <param name="value"></param>
        /// <returns>Always false to skip original method call</returns>
        [HarmonyPrefix]
        [HarmonyPatch(MethodType.Setter)]
        static bool Prefix(Campaign __instance, ref CampaignTimeControlMode value)
        {
            if (__instance != null && !__instance.TimeControlModeLock && 
                value != __instance.TimeControlMode && TimeControl.CanSyncTimeControlMode)
            {
                _timeControlModeRef(__instance) = value;
            }

            return false;
        }
    }

}