using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyHeroJoinedParty : IEvent
{
    public readonly MobileParty NewParty;
    public readonly Hero Companion;

    public NotifyHeroJoinedParty(
        MobileParty newParty,
        Hero companion)
    {
        NewParty = newParty;
        Companion = companion;
    }
}
