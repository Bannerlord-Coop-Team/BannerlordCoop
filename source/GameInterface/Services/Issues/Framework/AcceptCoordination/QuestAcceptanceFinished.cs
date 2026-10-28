using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

public readonly struct QuestAcceptanceFinished : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceFinished(QuestBase quest) => Quest = quest;
}

public readonly struct QuestAcceptanceJournalFlushRequested : IEvent
{
}

public readonly struct QuestAcceptanceStarted : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceStarted(QuestBase quest) => Quest = quest;
}
