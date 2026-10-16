using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyCleanHideoutRelations : IEvent
{
    public readonly Hero PlayerHero;
    public readonly int RelationValue;

    public NotifyCleanHideoutRelations(Hero playerHero, int relationValue)
    {
        PlayerHero = playerHero;
        RelationValue = relationValue;
    }
}
