using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party.Messages;

public readonly struct GarrisonDonated : IEvent
{
    public readonly Settlement CurrentSettlement;
    public readonly TroopRoster LeftMemberRoster;

    public GarrisonDonated(
        Settlement currentSettlement,
        TroopRoster leftMemberRoster)
    {
        CurrentSettlement = currentSettlement;
        LeftMemberRoster = leftMemberRoster;
    }
}