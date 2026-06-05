using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party.Messages;

public readonly struct PrisonersDonated : IEvent
{
    public readonly FlattenedTroopRoster RightSidePrisonerRoster;
    public readonly Settlement CurrentSettlement;
    public readonly PartyBase RightParty;

    public PrisonersDonated(
        FlattenedTroopRoster rightSidePrisonerRoster,
        Settlement currentSettlement,
        PartyBase rightParty)
    {
        RightSidePrisonerRoster = rightSidePrisonerRoster;
        CurrentSettlement = currentSettlement;
        RightParty = rightParty;
    }
}