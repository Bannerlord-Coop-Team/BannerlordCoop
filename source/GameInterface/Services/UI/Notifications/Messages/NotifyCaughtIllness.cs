using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyCaughtIllness : IEvent
{
    public readonly Hero PlayerHero;

    public NotifyCaughtIllness(Hero playerHero)
    {
        PlayerHero = playerHero;
    }
}
