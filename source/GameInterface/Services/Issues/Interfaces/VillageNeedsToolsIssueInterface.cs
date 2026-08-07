using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

public interface IVillageNeedsToolsIssueInterface : IGameAbstraction
{
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
