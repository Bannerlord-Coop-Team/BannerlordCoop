using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party.Messages;

public readonly struct GarrisonManaged : IEvent
{
    public readonly Settlement CurrentSettlement;
    public readonly TroopRoster LeftMemberRoster;
    public readonly TroopRoster LeftPrisonerRoster;

    public GarrisonManaged(
        Settlement currentSettlement,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonerRoster)
    {
        CurrentSettlement = currentSettlement;
        LeftMemberRoster = leftMemberRoster;
        LeftPrisonerRoster = leftPrisonerRoster;
    }
}