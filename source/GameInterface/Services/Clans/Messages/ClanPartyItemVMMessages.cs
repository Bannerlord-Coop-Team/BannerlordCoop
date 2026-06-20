using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Clans.Messages;

public readonly struct PartyBehaviorUpdatedOnSelection : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly MobileParty.PartyObjective PartyObjective;

    public PartyBehaviorUpdatedOnSelection(
        MobileParty mobileParty,
        MobileParty.PartyObjective partyObjective)
    {
        MobileParty = mobileParty;
        PartyObjective = partyObjective;
    }
}

public readonly struct AutoRecruitChangedForSettlement : IEvent
{
    public readonly Settlement HomeSettlement;
    public readonly bool Value;

    public AutoRecruitChangedForSettlement(Settlement homeSettlement, bool value)
    {
        HomeSettlement = homeSettlement;
        Value = value;
    }
}