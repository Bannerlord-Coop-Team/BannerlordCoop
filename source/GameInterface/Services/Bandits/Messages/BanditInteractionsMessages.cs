using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Bandits.Messages;

public readonly struct SetPlayerBanditInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty ConversationParty;
    public readonly BanditInteractionsCampaignBehavior.PlayerInteraction Interaction;

    public SetPlayerBanditInteraction(
        Hero mainHero,
        MobileParty conversationParty,
        BanditInteractionsCampaignBehavior.PlayerInteraction interaction)
    {
        MainHero = mainHero;
        ConversationParty = conversationParty;
        Interaction = interaction;
    }
}

public readonly struct BanditPartyScreenDoneCondition : IEvent
{
    public readonly TroopRoster RightMemberRoster;

    public BanditPartyScreenDoneCondition(TroopRoster rightMemberRoster)
    {
        RightMemberRoster = rightMemberRoster;
    }
}

public readonly struct GetBanditMemberAndPrisonerRosters : IEvent
{
    public readonly Clan PlayerClan;
    public readonly MobileParty MainParty;
    public readonly List<MobileParty> Parties;
    public readonly bool DoBanditsJoinPlayerSide;

    public GetBanditMemberAndPrisonerRosters(
        Clan playerClan,
        MobileParty mainParty,
        List<MobileParty> parties,
        bool doBanditsJoinPlayerSide)
    {
        PlayerClan = playerClan;
        MainParty = mainParty;
        Parties = parties;
        DoBanditsJoinPlayerSide = doBanditsJoinPlayerSide;
    }
}

public readonly struct RosterScreenAfterBanditEncounter : IEvent
{
    public readonly List<MobileParty> Parties;
    public readonly MobileParty MainParty;

    public RosterScreenAfterBanditEncounter(
        List<MobileParty> parties,
        MobileParty mainParty)
    {
        Parties = parties;
        MainParty = mainParty;
    }
}