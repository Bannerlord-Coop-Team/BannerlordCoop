using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Every completion path (success, timeout, cancel, betrayal, fail, stay-alive-conditions-failed, ...)
/// funnels through <see cref="IssueBase.IssueFinalized"/> (see the decompiled source: every
/// <c>CompleteIssueWithXxx</c> ends by calling it), which removes the issue from
/// <c>IssueManager.Issues</c> and clears the owning hero's <c>Issue</c> back-reference - neither of which
/// is covered by this project's existing per-field AutoSync (<c>Hero.Issue</c> is explicitly excluded in
/// <c>HeroSync</c>). This single choke point broadcasts that teardown to every peer regardless of which
/// specific consequence path triggered it, instead of needing a bespoke message per consequence method.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class IssueFinalizedPatches
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        // Skip a mirrored replay (Interfaces.VillageNeedsToolsIssueInterface.FinalizeMirror runs under
        // AllowedThread) so applying a received broadcast never re-triggers another broadcast.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue) return;

        MessageBroker.Instance.Publish(__instance, new VillageIssueFinalizedTriggered(__instance.IssueOwner));
    }
}
