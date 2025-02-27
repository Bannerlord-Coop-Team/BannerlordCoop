using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Patches
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
            return true;
        }

        [HarmonyPatch("RemoveStance")]
        [HarmonyPrefix]
        private static bool RemoveStancePrefix()
        {
            return true;
        }

        [HarmonyPatch("SetStance")]
        [HarmonyPrefix]
        private static bool SetStancePrefix()
        {
            return true;
        }

        [HarmonyPatch("DeclareAlliance")]
        [HarmonyPrefix]
        private static bool DeclareAlliancePrefix()
        {
            return true;
        }

        [HarmonyPatch("DeclareWar")]
        [HarmonyPrefix]
        private static bool DeclareWarPrefix()
        {
            return true;
        }

        [HarmonyPatch("SetNeutral")]
        [HarmonyPrefix]
        private static bool SetNeutralPrefix()
        {
            return true;
        }
    }
}
