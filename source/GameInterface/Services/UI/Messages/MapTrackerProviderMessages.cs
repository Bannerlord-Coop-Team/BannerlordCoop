using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Messages;

public readonly struct MapTrackerPartyCreated : IEvent
{
    public readonly MobileParty MobileParty;

    public MapTrackerPartyCreated(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}

public readonly struct MapTrackerPartyRemoved : IEvent
{
    public readonly MobileParty MobileParty;

    public MapTrackerPartyRemoved(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
