using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

public interface IGenericAcceptMirrorInterface : IGameAbstraction
{
    void MirrorQuestAccepted(Hero owner);

    void EnsureServerQuestMirror(Hero owner);

    void MirrorAlternativeAccepted(Hero owner, AlternativeSolutionVanillaState state);

    void RejectAcceptance(Hero owner);
}

public class GenericAcceptMirrorInterface : IGenericAcceptMirrorInterface
{
    public void MirrorQuestAccepted(Hero owner)
    {
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;

        using (new AllowedThread())
        {
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    public void EnsureServerQuestMirror(Hero owner)
    {
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        owner.Issue.StartIssueWithQuest();
    }

    public void MirrorAlternativeAccepted(Hero owner, AlternativeSolutionVanillaState state)
    {
        if (!GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(owner?.Issue) || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;

        using (new AllowedThread())
        {
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.IsTriedToSolveBefore = true;
            AlternativeSolutionVanillaStateSync.Apply(issue, state);
        }
    }

    public void RejectAcceptance(Hero owner) => AcceptMirrorSupport.RejectAcceptance(owner);
}
