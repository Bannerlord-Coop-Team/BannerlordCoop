using HarmonyLib;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return true;
        }

        [HarmonyPatch("RemoveStance")]
        [HarmonyPrefix]
        private static bool RemoveStancePrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch("SetStance")]
        [HarmonyPrefix]
        private static bool SetStancePrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch("DeclareWar")]
        [HarmonyPrefix]
        private static bool DeclareWarPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch("SetNeutral")]
        [HarmonyPrefix]
        private static bool SetNeutralPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}
