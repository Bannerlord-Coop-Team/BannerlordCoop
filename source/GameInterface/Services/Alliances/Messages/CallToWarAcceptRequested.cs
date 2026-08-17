using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct CallToWarAcceptRequested : IEvent
{
    public readonly Kingdom CallingKingdom;
    public readonly Kingdom CalledKingdom;
    public readonly Kingdom KingdomToCallToWarAgainst;
    public readonly Hero Player;
    public readonly bool IsPlayerPaying;

    public CallToWarAcceptRequested(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, Hero player, bool isPlayerPaying)
    {
        CallingKingdom = callingKingdom;
        CalledKingdom = calledKingdom;
        KingdomToCallToWarAgainst = kingdomToCallToWarAgainst;
        Player = player;
        IsPlayerPaying = isPlayerPaying;
    }
}