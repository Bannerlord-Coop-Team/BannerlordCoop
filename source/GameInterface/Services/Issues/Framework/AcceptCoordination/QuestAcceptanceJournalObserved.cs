using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

public readonly struct QuestAcceptanceJournalObserved : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceJournalObserved(QuestBase quest) => Quest = quest;
}

public readonly struct QuestAcceptanceStarted : IEvent
{
    public readonly QuestBase Quest;

    public QuestAcceptanceStarted(QuestBase quest) => Quest = quest;
}
