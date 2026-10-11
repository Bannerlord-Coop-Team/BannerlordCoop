using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct UpdateMemberRosterVersionAfterPerkChange : IEvent
{
    public readonly TroopRoster MemberRoster;

    public UpdateMemberRosterVersionAfterPerkChange(TroopRoster memberRoster)
    {
        MemberRoster = memberRoster;
    }
}
