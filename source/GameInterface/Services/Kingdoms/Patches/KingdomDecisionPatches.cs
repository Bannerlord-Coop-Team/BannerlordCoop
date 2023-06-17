using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
            return false;
        }

        [HarmonyPatch("ConsiderPolicy")]
        [HarmonyPostfix]
        public static void ConsiderPolicyPostfix(ref bool __result)
        {
            __result = false;
        }
    }
}
