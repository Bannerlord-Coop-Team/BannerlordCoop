using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.AnchorPoints.Messages;

public readonly struct CallFleetRequested : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly Settlement Settlement;

    public CallFleetRequested(
        MobileParty mobileParty,
        Settlement settlement)
    {
        MobileParty = mobileParty;
        Settlement = settlement;
    }
}
