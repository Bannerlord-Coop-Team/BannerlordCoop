using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct CallToWarOfferDenied : IEvent
{
    public readonly string CallingKingdom;
    public readonly string CalledKingdom;

    public CallToWarOfferDenied(Kingdom callingKingdom, Kingdom calledKingdom)
    {
        CallingKingdom = callingKingdom.StringId;
        CalledKingdom = calledKingdom.StringId;
    }
}