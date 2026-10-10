using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>
/// Local event published on the server after vanilla finished an issue.
/// </summary>
public readonly struct IssueOutcomeObserved : IEvent
{
    public readonly IssueBase Issue;
    public readonly IssueOutcome Outcome;

    public IssueOutcomeObserved(IssueBase issue, IssueOutcome outcome)
    {
        Issue = issue;
        Outcome = outcome;
    }
}
