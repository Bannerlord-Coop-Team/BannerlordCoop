using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyAnimalsSlaughteredToEat : IEvent
{
    public readonly MobileParty MobileParty;

    public NotifyAnimalsSlaughteredToEat(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
