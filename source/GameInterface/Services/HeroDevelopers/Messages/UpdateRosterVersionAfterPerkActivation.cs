using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct UpdateRosterVersionAfterPerkActivation : IEvent
{
    public readonly TroopRoster MemberRoster;

    public UpdateRosterVersionAfterPerkActivation(TroopRoster memberRoster)
    {
        MemberRoster = memberRoster;
    }
}
