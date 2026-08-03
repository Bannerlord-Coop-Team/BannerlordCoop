using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// <see cref="IssueManager.OnQuestCompleted"/> is the one place vanilla itself already knows exactly which
/// outcome a quest-driven finalize is (it's the method that decides which <c>IssueBase.CompleteIssueWithXxx</c>
/// to call next, immediately before that cascades into <see cref="IssueBase.IssueFinalized"/>). Captured here
/// and consumed by <see cref="IssueFinalizedPatches"/> so the broadcast it publishes carries a
/// <see cref="VillageIssueFinalizeReason"/> a mirroring peer can replay exactly - see the doc comment on
/// <see cref="IssueFinalizedPatches"/> for why that now matters (Finding 1's accept-sync fix means every peer
/// can have a real, active quest of its own to tear down correctly instead of orphaning).
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerQuestCompletedReasonCapture
{
    // Keyed by IssueOwner/QuestGiver (1 issue per hero, enforced by IssueManager.Issues) rather than by
    // IssueBase/QuestBase instance, so it's trivially readable from IssueFinalizedPatches.Postfix without
    // needing a back-reference from IssueBase to its own QuestBase's completion detail (IssueBase doesn't
    // expose one).
    public static readonly Dictionary<Hero, VillageIssueFinalizeReason> PendingReasons = new();

    // Runs for both a genuine quest completion and a mirrored replay of one (VillageNeedsToolsIssueInterface.
    // FinalizeMirror calls the real QuestBase.CompleteQuestWithXxx under AllowedThread) - harmless either way,
    // since a replay's own IssueFinalized postfix below no-ops on CallOriginalPolicy.IsOriginalAllowed().
    [HarmonyPatch(nameof(IssueManager.OnQuestCompleted))]
    [HarmonyPrefix]
    private static void Prefix(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (quest is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest || quest.QuestGiver == null) return;

        PendingReasons[quest.QuestGiver] = detail switch
        {
            QuestBase.QuestCompleteDetails.Success => VillageIssueFinalizeReason.QuestSuccess,
            QuestBase.QuestCompleteDetails.Cancel => VillageIssueFinalizeReason.QuestCancel,
            QuestBase.QuestCompleteDetails.Fail => VillageIssueFinalizeReason.QuestFail,
            QuestBase.QuestCompleteDetails.Timeout => VillageIssueFinalizeReason.QuestTimeout,
            QuestBase.QuestCompleteDetails.FailWithBetrayal => VillageIssueFinalizeReason.QuestBetrayal,
            _ => VillageIssueFinalizeReason.IssueOnly,
        };
    }
}

/// <summary>
/// Every completion path (success, timeout, cancel, betrayal, fail, stay-alive-conditions-failed, ...)
/// funnels through <see cref="IssueBase.IssueFinalized"/> (see the decompiled source: every
/// <c>CompleteIssueWithXxx</c> ends by calling it), which removes the issue from
/// <c>IssueManager.Issues</c> and clears the owning hero's <c>Issue</c> back-reference - neither of which
/// is covered by this project's existing per-field AutoSync (<c>Hero.Issue</c> is explicitly excluded in
/// <c>HeroSync</c>). This single choke point broadcasts that teardown to every peer regardless of which
/// specific consequence path triggered it, instead of needing a bespoke message per consequence method.
///
/// Since Finding 1's accept-sync fix (<see cref="IssueQuestAcceptancePatch"/>) means every peer can now have
/// a real, active <see cref="QuestBase"/> of its own for this issue, a bare <c>IssueFinalized()</c> replay is
/// no longer always correct on the mirroring side - it never calls the matching
/// <c>QuestBase.CompleteQuestWithXxx</c>, orphaning that peer's own quest object. The reason captured by
/// <see cref="IssueManagerQuestCompletedReasonCapture"/> is carried alongside the broadcast so
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface.FinalizeMirror"/> can replay the exact matching call.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class IssueFinalizedPatches
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        // Always drain any pending reason for this owner first, on every call (genuine or replayed) - a
        // mirrored replay also runs the real IssueBase/QuestBase methods (just under AllowedThread), so it
        // re-populates this exact same entry via IssueManagerQuestCompletedReasonCapture.Prefix too. If the
        // early-return below skipped the drain, a stale entry would leak into this hero's NEXT, unrelated
        // issue's genuine finalize and report the wrong reason for it.
        var owner = __instance.IssueOwner;
        var reason = VillageIssueFinalizeReason.IssueOnly;
        if (owner != null && IssueManagerQuestCompletedReasonCapture.PendingReasons.TryGetValue(owner, out var pending))
        {
            reason = pending;
            IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(owner);
        }

        // Skip a mirrored replay (Interfaces.VillageNeedsToolsIssueInterface.FinalizeMirror runs under
        // AllowedThread) so applying a received broadcast never re-triggers another broadcast.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue) return;

        MessageBroker.Instance.Publish(__instance, new VillageIssueFinalizedTriggered(owner, reason));
    }
}
