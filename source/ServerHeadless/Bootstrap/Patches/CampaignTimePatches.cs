using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="CampaignTime.Now"/> reads the native map-time clock. Headless, anchor it to zero.
    /// Ported from the Coop test harness.
    /// </summary>
    [HarmonyPatch(typeof(CampaignTime))]
    internal class CampaignTimePatches
    {
        [HarmonyPatch(nameof(CampaignTime.Now), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool GetNowPrefix(ref CampaignTime __result)
        {
            __result = CampaignTime.Zero;
            return false;
        }
    }
}
