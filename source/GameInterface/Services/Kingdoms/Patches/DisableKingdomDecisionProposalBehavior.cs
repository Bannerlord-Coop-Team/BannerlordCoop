using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
internal class DisableKingdomDecisionProposalBehavior
{
    [HarmonyPatch(nameof(KingdomDecisionProposalBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
