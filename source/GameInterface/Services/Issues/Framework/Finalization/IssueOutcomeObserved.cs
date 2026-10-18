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
    public readonly TaleWorlds.Localization.TextObject JournalLog;

    public IssueOutcomeObserved(IssueBase issue, IssueOutcome outcome, TaleWorlds.Localization.TextObject journalLog = null)
    {
        Issue = issue;
        Outcome = outcome;
        JournalLog = journalLog;
    }
}
