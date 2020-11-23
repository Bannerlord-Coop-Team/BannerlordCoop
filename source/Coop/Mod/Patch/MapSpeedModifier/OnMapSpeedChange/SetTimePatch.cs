using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnMapSpeedChange
{

    /// <summary>
    ///     <para>
    ///         Creates a flag when <c>Campaign::SetTimeSpeed</c> gets called.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/113">issue #113</see>
    ///         and check <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see> for more information.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes <c>Campaign::SetTimeSpeed</c> to add a flag to know
    ///     when the user have changed the map speed.
    /// </remarks>
    [HarmonyPatch(typeof(Campaign))]
    [HarmonyPatch("SetTimeSpeed")]
    class SetTimePatch
    {
        /// <summary>
        /// Determine if the user has changed the map speed using input keys.
        /// </summary>
        public static bool CanChangeSpeedControl = false;


        static void Prefix()
        {

            CanChangeSpeedControl = true;

        }

    }
}
