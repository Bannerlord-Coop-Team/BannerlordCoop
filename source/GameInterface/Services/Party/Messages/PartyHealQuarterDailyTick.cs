using Common.Messaging;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Messages;

public readonly struct PartyHealQuarterDailyTick : IEvent
{
    public readonly PartyHealCampaignBehavior PartyHealCampaignBehavior;
    public readonly MobileParty MobileParty;

    public PartyHealQuarterDailyTick(
        PartyHealCampaignBehavior partyHealCampaignBehavior,
        MobileParty mobileParty)
    {
        PartyHealCampaignBehavior = partyHealCampaignBehavior;
        MobileParty = mobileParty;
    }
}