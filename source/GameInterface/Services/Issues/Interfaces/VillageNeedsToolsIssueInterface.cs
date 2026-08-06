using Common.Util;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>Shared accept-mirror/finalize surface used by ~30 issue types (see <see cref="GenericAcceptMirrorIssueTypes"/>), not exclusive to VillageNeedsToolsIssue despite the name.</summary>
public interface IVillageNeedsToolsIssueInterface : IGameAbstraction
{
    /// <summary>Mirrors a finalize broadcast under <see cref="AllowedThread"/>; no-op if the hero has no issue.</summary>
    void FinalizeMirror(Hero owner, VillageIssueFinalizeReason reason);

    /// <summary>Force-writes the issue state to SolvingWithQuestSolution on a non-accepting peer; deliberately leaves <c>IssueQuest</c> null (safe - see <see cref="EnsureServerQuestMirror"/> for the one exception). No-op if not <c>IsOngoingWithoutQuest</c>.</summary>
    void MirrorQuestAccepted(Hero owner);

    /// <summary>Server-only: builds a real Quest even when the server isn't the recorded owner, since it still needs one for party-spawn/battle arbitration on the owner's behalf. No-op if not <c>IsOngoingWithoutQuest</c> or not mirror-eligible.</summary>
    void EnsureServerQuestMirror(Hero owner);

    /// <summary>
    /// Force-writes issue state to SolvingWithAlternativeSolution on a non-accepting peer. No-op if not <c>IsOngoingWithoutQuest</c>.
    /// Deliberately leaves <c>AlternativeSolutionReturnTimeForTroops</c> untouched - permanently overwriting it here would get resynced back onto the real owner on their next reconnect and silently strand their troops/reward (see <see cref="Patches.VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/> for how the real consequence is actually gated instead).
    /// </summary>
    void MirrorAlternativeAccepted(Hero owner);

    /// <summary>Rolls back a losing peer's own optimistic accept after the server reports another peer won the same-issue race. No-op if an owner is already recorded (a legitimate mirror already superseded it) or the issue is still <c>IsOngoingWithoutQuest</c>.</summary>
    void RejectAcceptance(Hero owner);
}

/// <inheritdoc cref="IVillageNeedsToolsIssueInterface"/>
public class VillageNeedsToolsIssueInterface : IVillageNeedsToolsIssueInterface
{
    // Private nested enum - reflected once, statically, rather than per-call.
    private static readonly Type IssueStateEnumType =
        AccessTools.Inner(typeof(IssueBase), "IssueState");
    private static readonly FieldInfo IssueStateField =
        AccessTools.Field(typeof(IssueBase), "_issueState");
    private static readonly object SolvingWithAlternativeSolutionStateValue =
        Enum.Parse(IssueStateEnumType, "SolvingWithAlternativeSolution");
    private static readonly object SolvingWithQuestSolutionStateValue =
        Enum.Parse(IssueStateEnumType, "SolvingWithQuestSolution");
    private static readonly PropertyInfo IsTriedToSolveBeforeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IsTriedToSolveBefore));

    public void FinalizeMirror(Hero owner, VillageIssueFinalizeReason reason)
    {
        if (owner?.Issue == null) return;

        using (new AllowedThread())
        {
            var quest = owner.Issue.IssueQuest;
            if (quest != null && quest.IsOngoing)
            {
                switch (reason)
                {
                    case VillageIssueFinalizeReason.QuestSuccess:
                        quest.CompleteQuestWithSuccess();
                        return;
                    case VillageIssueFinalizeReason.QuestCancel:
                        quest.CompleteQuestWithCancel();
                        return;
                    case VillageIssueFinalizeReason.QuestFail:
                        quest.CompleteQuestWithFail();
                        return;
                    case VillageIssueFinalizeReason.QuestTimeout:
                        quest.CompleteQuestWithTimeOut();
                        return;
                    case VillageIssueFinalizeReason.QuestBetrayal:
                        quest.CompleteQuestWithBetrayal();
                        return;
                    default:
                        // Reason didn't say how an active quest ended - fail safe to cancel rather than orphan it.
                        quest.CompleteQuestWithCancel();
                        return;
                }
            }

            if (reason == VillageIssueFinalizeReason.RejectedAccept)
            {
                owner.Issue.CompleteIssueWithCancel();
                return;
            }

            owner.Issue.IssueFinalized();
        }
    }

    public void MirrorQuestAccepted(Hero owner)
    {
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;

        using (new AllowedThread())
        {
            IssueStateField.SetValue(issue, SolvingWithQuestSolutionStateValue);
            IsTriedToSolveBeforeProperty.SetValue(issue, true);
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    public void EnsureServerQuestMirror(Hero owner)
    {
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        owner.Issue.StartIssueWithQuest();
    }

    public void MirrorAlternativeAccepted(Hero owner)
    {
        if (!GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;

        using (new AllowedThread())
        {
            IssueStateField.SetValue(issue, SolvingWithAlternativeSolutionStateValue);
            IsTriedToSolveBeforeProperty.SetValue(issue, true);
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;

        // Ownership is only ever recorded from the server's authoritative broadcast, never optimistically by
        // this peer's own accept trigger - so "no owner recorded yet" means owner.Issue is still this peer's
        // own, never-mirrored, optimistic accept (safe to roll back). Once an owner IS recorded, a legitimate
        // mirror already superseded it and must be left alone (see
        // RequestVillageIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer).
        if (VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }
}
