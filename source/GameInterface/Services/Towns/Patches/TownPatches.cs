using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches
{
    /// <summary>
    /// Disables all functionality for Town
    /// </summary>
    [HarmonyPatch(typeof(Town))]
    internal class TownPatches
    {
        [HarmonyPatch("DailyTick")]
        [HarmonyPrefix]
        private static bool DailyTickPrefix()
        {
            return false;
        }
    }
}
