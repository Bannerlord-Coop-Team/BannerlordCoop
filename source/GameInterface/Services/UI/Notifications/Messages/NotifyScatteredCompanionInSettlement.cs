using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyScatteredCompanionInSettlement : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly Hero Companion;

    public NotifyScatteredCompanionInSettlement(
        MobileParty mobileParty,
        Hero companion)
    {
        MobileParty = mobileParty;
        Companion = companion;
    }
}
