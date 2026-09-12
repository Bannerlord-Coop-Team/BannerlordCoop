using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct CallToWarOfferDenied : IEvent
{
    public readonly Kingdom CallingKingdom;
    public readonly Kingdom CalledKingdom;

    public CallToWarOfferDenied(Kingdom callingKingdom, Kingdom calledKingdom)
    {
        CallingKingdom = callingKingdom;
        CalledKingdom = calledKingdom;
    }
}