using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class DisableHeadmanNeedsGrainIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
