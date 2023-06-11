using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.FactionManagerService.Patches
{
    /// <summary>
    /// Disables all functionality for <see cref="FactionManager"/>
    /// </summary>
    [HarmonyPatch(typeof(FactionManager))]
    internal class FactionManagerPatches
    {
        [HarmonyPatch("AddStance")]
        [HarmonyPrefix]
        private static bool AddStancePrefix()
        {
            return false;
        }

        [HarmonyPatch("RemoveStance")]
        [HarmonyPrefix]
        private static bool RemoveStancePrefix()
        {
            return false;
        }

        [HarmonyPatch("SetStance")]
        [HarmonyPrefix]
        private static bool SetStancePrefix()
        {
            return false;
        }
    }
}
