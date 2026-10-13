using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.AnchorPoints.Messages;

public readonly struct EmbarkAtAnchorRequested : IEvent
{
    public readonly MobileParty MobileParty;

    public EmbarkAtAnchorRequested(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
