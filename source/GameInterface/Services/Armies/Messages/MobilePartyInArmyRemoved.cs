using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is removed from an Army
/// </summary>
public readonly struct MobilePartyInArmyRemoved : IEvent
{
    public readonly Army Army;
    public readonly MobileParty MobileParty;
    public readonly MobileParty ClientMobileParty;
    public readonly bool AbandonArmy;

    public MobilePartyInArmyRemoved(Army army, MobileParty mobileParty, MobileParty clientMobileParty, bool abandonArmy=false)
    {
        Army = army;
        MobileParty = mobileParty;
        ClientMobileParty = clientMobileParty;
        AbandonArmy = abandonArmy;
    }
}
