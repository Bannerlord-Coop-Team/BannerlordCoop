using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.CampaignPatches
{

    /// <summary>
    ///     <para>
    ///         Code patcher through Harmony which overrides new campaign map time speed on its setter.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see> and <seealso href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/133">issue #133</seealso>
    ///     </para>
    ///     <para>
    ///         Check <seealso cref="SetTimeSpeedPatch"/> too, it uses the flag <see cref="TimeControl.CanSyncTimeControlMode"/>
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes setter of <see cref="TaleWorlds.CampaignSystem.Campaign.TimeControlMode"/> to change its behavior 
    ///     and change its value just after <see cref="TaleWorlds.CampaignSystem.Campaign.SetTimeSpeed"/> is called
    /// </remarks>
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.TimeControlMode))]
    static class TimeControlModePatch
    {
        /// <summary>
        /// Grants access to the private field of the property <see cref="TaleWorlds.CampaignSystem.Campaign.TimeControlMode"/> used to override the setter logic.
        /// </summary>
        static readonly AccessTools.FieldRef<Campaign, CampaignTimeControlMode> _timeControlModeRef =
            AccessTools.FieldRefAccess<Campaign, CampaignTimeControlMode>("_timeControlMode");

        /// <summary>
        /// Overrides <see cref="TaleWorlds.CampaignSystem.Campaign.TimeControlMode"/> setter logic
        /// </summary>
        /// <param name="__instance">Current Campaign</param>
        /// <param name="value">Reference to the new value</param>
        /// <returns>Always false to skip original method call</returns>
        [HarmonyPrefix]
        [HarmonyPatch(MethodType.Setter)]
        static bool Prefix(Campaign __instance, ref CampaignTimeControlMode value)
        {
            //if (__instance != null && !__instance.TimeControlModeLock &&
            //    value != __instance.TimeControlMode && TimeControl.CanSyncTimeControlMode)
            //{
            //    _timeControlModeRef(__instance) = value;
            //}

            return false;
        }
    }

}