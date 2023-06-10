using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Policy.Patches
{
    /// <summary>
    /// Disables functionality of policies in game.
    /// </summary>
    /// <seealso cref="TaleWorlds.CampaignSystem.PolicyObject"/>
    [HarmonyPatch(typeof(Kingdom))]
    internal class KingdomPatches
    {
        [HarmonyPatch("AddDecision")]
        [HarmonyPrefix]
        public static bool AddDecisionPrefix()
        {
            return false;
        }

        [HarmonyPatch("RemoveDecision")]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix()
        {
            return false;
        }

        [HarmonyPatch("AddPolicy")]
        [HarmonyPrefix]
        public static bool AddPolicyPrefix()
        {
            return false;
        }

        [HarmonyPatch("RemovePolicy")]
        [HarmonyPrefix]
        public static bool RemovePolicyPrefix()
        {
            return false;
        }
    }
}
