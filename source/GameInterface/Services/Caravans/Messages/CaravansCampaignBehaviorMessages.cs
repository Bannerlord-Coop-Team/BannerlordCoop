using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Caravans.Messages;

public readonly struct CaravansKingdomDestroyed : IEvent
{
    public readonly Kingdom DestroyedKingdom;

    public CaravansKingdomDestroyed(Kingdom destroyedKingdom)
    {
        DestroyedKingdom = destroyedKingdom;
    }
}

public readonly struct CaravanPartyDestroyed : IEvent
{
    public readonly MobileParty MobileParty;

    public CaravanPartyDestroyed(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
