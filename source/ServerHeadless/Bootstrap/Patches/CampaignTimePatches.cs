using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="CampaignTime.Now"/> reads <c>Campaign.Current.MapTimeTracker.Now</c>, which can be
    /// null very early in load before the tracker is deserialized. Fall back to zero only then;
    /// otherwise use the real map time so the campaign clock advances when ticking.
    /// </summary>
    [HarmonyPatch(typeof(CampaignTime))]
    internal class CampaignTimePatches
    {
        [HarmonyPatch(nameof(CampaignTime.Now), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool GetNowPrefix(ref CampaignTime __result)
        {
            if (Campaign.Current?.MapTimeTracker == null)
            {
                __result = CampaignTime.Zero;
                return false; // skip original (would NRE)
            }
            return true; // let the real getter run
        }
    }
}
