using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

public readonly struct QuestAcceptanceFinished : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceFinished(QuestBase quest) => Quest = quest;
}

public readonly struct QuestSolutionAcceptedOnServer : IEvent
{
    public readonly string IssueOwnerId;
    public readonly string IssueId;
    public readonly string ControllerId;

    public QuestSolutionAcceptedOnServer(string issueOwnerId, string issueId, string controllerId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}

public readonly struct QuestSolutionAcceptedOnClient : IEvent
{
    public readonly IssueBase Issue;
    public readonly string ControllerId;

    public QuestSolutionAcceptedOnClient(IssueBase issue, string controllerId)
    {
        Issue = issue;
        ControllerId = controllerId;
    }
}

public readonly struct AlternativeSolutionAcceptedOnServer : IEvent
{
    public readonly string IssueOwnerId;
    public readonly string IssueId;
    public readonly string ControllerId;

    public AlternativeSolutionAcceptedOnServer(string issueOwnerId, string issueId, string controllerId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}

public readonly struct QuestAcceptanceJournalFlushRequested : IEvent
{
}

public readonly struct QuestAcceptanceStarted : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceStarted(QuestBase quest) => Quest = quest;
}
