using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableIssuesCampaignBehavior
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
