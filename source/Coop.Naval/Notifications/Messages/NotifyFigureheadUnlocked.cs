using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.Notifications.Messages;

public readonly struct NotifyFigureheadUnlocked : IEvent
{
    public readonly Hero PlayerHero;
    public readonly Figurehead Figurehead;

    public NotifyFigureheadUnlocked(
        Hero playerHero,
        Figurehead figurehead)
    {
        PlayerHero = playerHero;
        Figurehead = figurehead;
    }
}
