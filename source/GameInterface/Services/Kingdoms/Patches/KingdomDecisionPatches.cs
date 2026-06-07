using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables functionality of policies in game.
    /// </summary>
    /// <seealso cref="TaleWorlds.CampaignSystem.PolicyObject"/>
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
    internal class KingdomDecisionPatches
    {
        [HarmonyPatch("ConsiderPolicy")]
        [HarmonyPrefix]
        public static bool ConsiderPolicyPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch("ConsiderPolicy")]
        [HarmonyPostfix]
        public static void ConsiderPolicyPostfix(ref bool __result)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return;
            __result = false;
        }
    }
}
