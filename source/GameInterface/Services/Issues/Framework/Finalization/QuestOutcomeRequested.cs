using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>
/// Local event published on a client when its quest is about to end, the end is left to the server.
/// </summary>
public readonly struct QuestOutcomeRequested : IEvent
{
    public readonly QuestBase Quest;
    public readonly IssueOutcome Outcome;

    public QuestOutcomeRequested(QuestBase quest, IssueOutcome outcome)
    {
        Quest = quest;
        Outcome = outcome;
    }
}
