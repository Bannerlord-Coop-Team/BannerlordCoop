using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

public interface IVillageNeedsToolsIssueInterface : IGameAbstraction
{
    void FinalizeMirror(Hero owner, VillageIssueFinalizeReason reason);

    void MirrorQuestAccepted(Hero owner);

    void EnsureServerQuestMirror(Hero owner);

    void MirrorAlternativeAccepted(Hero owner);

    void RejectAcceptance(Hero owner);
}

public class VillageNeedsToolsIssueInterface : IVillageNeedsToolsIssueInterface
{
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

    public void RejectAcceptance(Hero owner) => AcceptMirrorSupport.RejectAcceptance(owner);
}
