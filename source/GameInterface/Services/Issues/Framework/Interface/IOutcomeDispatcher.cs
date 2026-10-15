using GameInterface.Services.Issues.Framework.Finalization;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>
/// The decisions the patches on vanilla's outcome methods make
/// </summary>
public interface IOutcomeDispatcher
{
    // Whether vanilla may run the quest outcome. A client hands it to the server instead.
    // observed is the issue the server must announce after the outcome ran, null when nothing is to be announced.
    bool BeforeQuestOutcome(QuestBase quest, IssueOutcome outcome, out IssueBase observed);

    // Same for the outcomes vanilla finishes an issue with when it has no quest
    bool BeforeIssueOutcome(IssueBase issue, IssueOutcome outcome, out IssueBase observed);

    bool BeforeQuestBranch(QuestBase quest, byte proof);

    // Only the server completes an alternative solution, as the owning player so vanilla finds a main hero
    bool BeforeAlternativeSolutionCompletion(IssueBase issue, out IDisposable owner);

    void After(IssueBase observed, IssueOutcome outcome);

    void AfterAlternativeSolutionCompletion(IssueBase issue, IDisposable owner);
}
