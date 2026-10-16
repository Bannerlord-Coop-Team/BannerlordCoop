using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

// Kept for the legacy Weapons finalization patches. RegisterEvents gating lives in the framework gate.
internal static class DisableAllIssueBehaviorsExceptAllowlist
{
    internal static bool IsAllowlisted(IssueBase issue)
    {
        return issue != null && QuestTypeRegistry.IsRegistered(issue.GetType());
    }
}