using HarmonyLib;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnMapSpeedChange
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
    [HarmonyPatch(typeof(Campaign))]
    [HarmonyPatch(nameof(Campaign.TimeControlMode), MethodType.Setter)]
    class TimeControlOverrider
    {
        /// <summary>
        /// Grants access to the private field <c>TaleWorlds.CampaignSystem.Campaign._timeControlMode</c> to override the setter logic.
        /// </summary>
        private static readonly FieldAccess _timeControlMode = new FieldAccess<CampaignTimeControlMode, CampaignTimeControlMode>(typeof(Campaign)
                        .GetField("_timeControlMode", AccessTools.all));

        /// <summary>
        /// Overrides the <c>TaleWorlds.CampaignSystem.Campaign.TimeControlMode</c> setter logic
        /// </summary>
        /// <param name="value"></param>
        /// <returns>Always false to skip original method call</returns>
        static bool Prefix(ref CampaignTimeControlMode value)
        {
            if (ShouldSetTimeControl(value))
            {
                _timeControlMode.Set(Campaign.Current, MapSpeedResolver.Resolve(value, true));

                SetTimePatch.CanChangeSpeedControl = false;
            }

            return false;
        }

        /// <summary>
        /// Method to know if the TimeControlMode should be changed.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>true if the TimeControlMode should be changed, false otherwise.</returns>
        private static bool ShouldSetTimeControl(CampaignTimeControlMode value)
        {
            return SetTimePatch.CanChangeSpeedControl && !Campaign.Current.TimeControlModeLock && value != Campaign.Current.TimeControlMode;
        }
    }

}