using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyDailyGoldChange : IEvent
{
    public readonly Hero Hero;
    public readonly int GoldChange;

    public NotifyDailyGoldChange(Hero hero, int goldChange)
    {
        Hero = hero;
        GoldChange = goldChange;
    }
}
