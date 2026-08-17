using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Messages;

public readonly struct RequestStanceLinkConstructed : IEvent
{
    public readonly StanceLink StanceLink;

    public RequestStanceLinkConstructed(StanceLink stanceLink)
    {
        StanceLink = stanceLink;
    }
}