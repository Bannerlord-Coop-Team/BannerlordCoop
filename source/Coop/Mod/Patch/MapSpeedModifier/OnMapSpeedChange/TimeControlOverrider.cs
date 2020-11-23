using HarmonyLib;
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
        static void Prefix(ref CampaignTimeControlMode value)
        {

            var newSpeed = MapSpeedResolver.Resolve(value, true);

            value = newSpeed;

        }

    }

}