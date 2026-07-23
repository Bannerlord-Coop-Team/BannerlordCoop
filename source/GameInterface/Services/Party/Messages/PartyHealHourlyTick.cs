using Common.Messaging;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Party.Messages;

public readonly struct PartyHealHourlyTick : IEvent
{
    public readonly PartyHealCampaignBehavior PartyHealCampaignBehavior;

    public PartyHealHourlyTick(PartyHealCampaignBehavior partyHealCampaignBehavior)
    {
        PartyHealCampaignBehavior = partyHealCampaignBehavior;
    }
}