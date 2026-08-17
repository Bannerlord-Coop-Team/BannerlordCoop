using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Messages;

public readonly struct StanceLinkDeconstructed : IEvent
{
    public readonly IFaction Faction1;
    public readonly StanceLink[] RemovedStanceLink;

    public StanceLinkDeconstructed(IFaction faction1, StanceLink[] removedStanceLink)
    {
        Faction1 = faction1;
        RemovedStanceLink = removedStanceLink;
    }
}
