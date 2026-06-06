using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a MobileParty is added to an Army
/// </summary>
public readonly struct MobilePartyInArmyAdded : IEvent
{
    public readonly Army Army;
    public readonly MobileParty MobileParty;

    public MobilePartyInArmyAdded(Army army, MobileParty mobileParty)
    {
        Army = army;
        MobileParty = mobileParty;
    }
}
