using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct UpdateRosterVersionAfterPerkChange : IEvent
{
    public readonly TroopRoster MemberRoster;

    public UpdateRosterVersionAfterPerkChange(TroopRoster memberRoster)
    {
        MemberRoster = memberRoster;
    }
}
