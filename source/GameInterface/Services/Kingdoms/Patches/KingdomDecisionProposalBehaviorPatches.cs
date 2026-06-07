using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Kingdoms.Patches
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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}
