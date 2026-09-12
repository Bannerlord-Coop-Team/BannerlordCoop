using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyRelationDecreasedByExecution : IEvent
{
    public readonly Hero Killer;
    public readonly Clan Clan;
    public readonly int Value;
    public readonly int RelationChange;

    public NotifyRelationDecreasedByExecution(
        Hero killer,
        Clan clan,
        int value,
        int relationChange)
    {
        Killer = killer;
        Clan = clan;
        Value = value;
        RelationChange = relationChange;
    }
}
