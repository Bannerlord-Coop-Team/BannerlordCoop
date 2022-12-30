using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.CampaignPatches
{

    /// <summary>
    ///     <para>
    ///         Creates a flag when <see cref="TaleWorlds.CampaignSystem.Campaign.SetTimeSpeed"/> is called.
    ///     </para>
    ///     <para>
    ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/113">issue #113</see>
    ///         and check <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see> for more information.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     This class prefixes <see cref="TaleWorlds.CampaignSystem.Campaign.SetTimeSpeed"/> to add a flag and know
    ///     when the user have changed the map speed.
    /// </remarks>
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.SetTimeSpeed))]
    static class SetTimeSpeedPatch
    {

        [HarmonyPrefix]
        static void Prefix(Campaign __instance, int speed)
        {

            if (ShouldEnableTimeControlMode(__instance.TimeControlMode, speed))
            {
                //TimeControl.CanSyncTimeControlMode = true;
            }

        }

        private static bool ShouldEnableTimeControlMode(CampaignTimeControlMode campaignTimeControlMode, int speed)
        {
            switch (campaignTimeControlMode)
            {
                case CampaignTimeControlMode.Stop:
                case CampaignTimeControlMode.FastForwardStop:
                    return speed != 0;
                case CampaignTimeControlMode.StoppablePlay:
                case CampaignTimeControlMode.UnstoppablePlay:
                    return speed != 1;
                case CampaignTimeControlMode.StoppableFastForward:
                case CampaignTimeControlMode.UnstoppableFastForward:
                case CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime:
                    return speed != 2;
                default:
                    return false;
            }
        }

    }
}
