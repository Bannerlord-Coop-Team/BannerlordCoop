using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyRelationDecreasedByExecutionSummary : IEvent
{
    public readonly Hero Killer;
    public readonly int NumberOfClans;

    public NotifyRelationDecreasedByExecutionSummary(
        Hero killer,
        int numberOfClans)
    {
        Killer = killer;
        NumberOfClans = numberOfClans;
    }
}
