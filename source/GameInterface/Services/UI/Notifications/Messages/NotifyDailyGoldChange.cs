using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyDailyGoldChange : IEvent
{
    public readonly Clan Clan;
    public readonly int GoldChange;

    public NotifyDailyGoldChange(Clan clan, int goldChange)
    {
        Clan = clan;
        GoldChange = goldChange;
    }
}
