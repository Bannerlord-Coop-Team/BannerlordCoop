using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyMoraleLossDueToFunds : IEvent
{
    public readonly MobileParty MobileParty;

    public NotifyMoraleLossDueToFunds(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
