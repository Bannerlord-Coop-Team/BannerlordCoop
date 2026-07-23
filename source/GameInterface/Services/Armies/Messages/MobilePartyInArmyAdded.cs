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
    public readonly bool AddPartyToMergedPartiesBool;

    public MobilePartyInArmyAdded(Army army, MobileParty mobileParty, bool addPartyToMergedPartiesBool)
    {
        Army = army;
        MobileParty = mobileParty;
        AddPartyToMergedPartiesBool = addPartyToMergedPartiesBool;
    }
}
