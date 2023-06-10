using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.KingdomDecisions.Patches
{
    /// <summary>
    /// Disables functionality of <see cref="KingdomDecisionProposalBehavior"/>
    /// </summary>
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
    internal class KingdomDecisionProposalBehaviorPatches
    {
        [HarmonyPatch("RegisterEvents")]
        [HarmonyPrefix]
        static bool RegisterEventsSkip()
        {
            return false;
        }
    }
}
