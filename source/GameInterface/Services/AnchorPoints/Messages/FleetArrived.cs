using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.AnchorPoints.Messages;

public readonly struct FleetArrived : IEvent
{
    public readonly MobileParty MobileParty;

    public FleetArrived(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
