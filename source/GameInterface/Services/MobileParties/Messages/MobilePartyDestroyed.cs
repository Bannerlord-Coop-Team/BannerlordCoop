using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct MobilePartyDestroyed : IEvent
{
    public readonly MobileParty MobileParty;

    public MobilePartyDestroyed(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
