using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Messages;

public readonly struct NewClanPartyScreenClosed : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero NewLeaderHero;
    public readonly TroopRoster LeftMemberRoster;
    public readonly TroopRoster LeftPrisonRoster;

    public NewClanPartyScreenClosed(
        Hero mainHero,
        Hero newLeaderHero,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonRoster)
    {
        MainHero = mainHero;
        NewLeaderHero = newLeaderHero;
        LeftMemberRoster = leftMemberRoster;
        LeftPrisonRoster = leftPrisonRoster;
    }
}