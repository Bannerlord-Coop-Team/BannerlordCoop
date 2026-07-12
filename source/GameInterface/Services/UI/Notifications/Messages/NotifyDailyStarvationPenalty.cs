using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyDailyStarvationPenalty : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly int DailyStarvationMoralePenalty;

    public NotifyDailyStarvationPenalty(
        MobileParty mobileParty,
        int dailyStarvationMoralePenalty)
    {
        MobileParty = mobileParty;
        DailyStarvationMoralePenalty = dailyStarvationMoralePenalty;
    }
}
