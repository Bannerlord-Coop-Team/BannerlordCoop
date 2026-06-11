using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Companions.Messages;

public readonly struct ClanNameSelectionDone : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero OneToOneConversationHero;
    public readonly Settlement SelectedFief;
    public readonly MobileParty MainParty;
    public readonly string ClanName;

    public ClanNameSelectionDone(
        Hero mainHero,
        Hero oneToOneConversationHero,
        Settlement selectedFief,
        MobileParty mainParty,
        string clanName)
    {
        MainHero = mainHero;
        OneToOneConversationHero = oneToOneConversationHero;
        SelectedFief = selectedFief;
        MainParty = mainParty;
        ClanName = clanName;
    }
}

public readonly struct CompanionFired : IEvent
{
    public readonly Hero OneToOneConversationHero;

    public CompanionFired(Hero oneToOneConversationHero)
    {
        OneToOneConversationHero = oneToOneConversationHero;
    }
}

public readonly struct CompanionRejoinAfterEmprisonment : IEvent
{
    public readonly Hero OneToOneConversationHero;
    public readonly MobileParty MainParty;

    public CompanionRejoinAfterEmprisonment(
        Hero oneToOneConversationHero,
        MobileParty mainParty)
    {
        OneToOneConversationHero = oneToOneConversationHero;
        MainParty = mainParty;
    }
}

public readonly struct CompanionJoinedPartyByRescue : IEvent
{
    public readonly Hero OneToOneConversationHero;
    public readonly MobileParty MainParty;

    public CompanionJoinedPartyByRescue(
        Hero oneToOneConversationHero,
        MobileParty mainParty)
    {
        OneToOneConversationHero = oneToOneConversationHero;
        MainParty = mainParty;
    }
}

public readonly struct PartyScreenClosedFromRescuing : IEvent
{
    public readonly PartyBase LeftOwnerParty;
    public readonly TroopRoster LeftMemberRoster;
    public readonly TroopRoster LeftPrisonRoster;
    public readonly PartyBase RightOwnerParty;
    public readonly TroopRoster RightMemberRoster;
    public readonly TroopRoster RightPrisonRoster;

    public PartyScreenClosedFromRescuing(
        PartyBase leftOwnerParty,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonRoster,
        PartyBase rightOwnerParty,
        TroopRoster rightMemberRoster,
        TroopRoster rightPrisonRoster)
    {
        LeftOwnerParty = leftOwnerParty;
        LeftMemberRoster = leftMemberRoster;
        LeftPrisonRoster = leftPrisonRoster;
        RightOwnerParty = rightOwnerParty;
        RightMemberRoster = rightMemberRoster;
        RightPrisonRoster = rightPrisonRoster;
    }
}

public readonly struct CompanionRescued : IEvent
{
    public readonly Hero OneToOneConversationHero;

    public CompanionRescued(Hero oneToOneConversationHero)
    {
        OneToOneConversationHero = oneToOneConversationHero;
    }
}