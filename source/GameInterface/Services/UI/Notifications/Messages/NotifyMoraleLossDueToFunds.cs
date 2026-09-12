using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyMoraleLossDueToFunds : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly float MoraleChange;

    public NotifyMoraleLossDueToFunds(MobileParty mobileParty, float moraleChange)
    {
        MobileParty = mobileParty;
        MoraleChange = moraleChange;
    }
}
