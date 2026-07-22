using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyGoldPlundered : IEvent
{
    public readonly Hero LeaderHero;
    public readonly int PlunderedGold;

    public NotifyGoldPlundered(Hero leaderHero, int plunderedGold)
    {
        LeaderHero = leaderHero;
        PlunderedGold = plunderedGold;
    }
}
