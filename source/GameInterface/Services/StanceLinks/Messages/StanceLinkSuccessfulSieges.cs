using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Messages;

public readonly struct StanceLinkSuccessfulSieges : IEvent
{
    public readonly StanceLink StanceLink;
    public readonly int Value;
    public readonly int Side;

    public StanceLinkSuccessfulSieges(StanceLink stanceLink, int value, int side)
    {
        StanceLink = stanceLink;
        Value = value;
        Side = side;
    }
}
