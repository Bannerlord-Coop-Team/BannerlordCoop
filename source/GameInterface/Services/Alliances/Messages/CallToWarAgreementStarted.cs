using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct CallToWarAgreementStarted : IEvent
{
    public readonly Kingdom CallingKingdom;
    public readonly Kingdom CalledKingdom;
    public readonly Kingdom KingdomToCallToWarAgainst;

    public CallToWarAgreementStarted(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        CallingKingdom = callingKingdom;
        CalledKingdom = calledKingdom;
        KingdomToCallToWarAgainst = kingdomToCallToWarAgainst;
    }
}
