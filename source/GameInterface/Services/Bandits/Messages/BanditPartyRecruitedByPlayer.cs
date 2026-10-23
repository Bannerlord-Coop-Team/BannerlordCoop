using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Bandits.Messages;

internal class BanditPartyRecruitedByPlayer : IEvent
{
    public MobileParty Bandits { get; }
    public MobileParty Recruiter { get; }

    public BanditPartyRecruitedByPlayer(MobileParty bandits, MobileParty recruiter)
    {
        Bandits = bandits;
        Recruiter = recruiter;
    }
}
