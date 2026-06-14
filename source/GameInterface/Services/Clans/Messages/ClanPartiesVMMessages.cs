using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Clans.Messages;

public readonly struct NewClanPartyCreated : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero NewLeader;
    public readonly Clan TargetClan;
    public readonly int PartyGoldLowerThreshold;

    public NewClanPartyCreated(
        Hero mainHero,
        Hero newLeader,
        Clan targetClan,
        int partyGoldLowerThreshold)
    {
        MainHero = mainHero;
        NewLeader = newLeader;
        TargetClan = targetClan;
        PartyGoldLowerThreshold = partyGoldLowerThreshold;
    }
}

public readonly struct ClanPartyLeaderChanged : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero NewLeader;
    public readonly Hero OldLeader;
    public readonly MobileParty SelectedParty;
    public readonly MobileParty MainParty;

    public ClanPartyLeaderChanged(
        Hero mainHero,
        Hero newLeader,
        Hero oldLeader,
        MobileParty selectedParty,
        MobileParty mainParty)
    {
        MainHero = mainHero;
        NewLeader = newLeader;
        OldLeader = oldLeader;
        SelectedParty = selectedParty;
        MainParty = mainParty;
    }
}

public readonly struct ClanPartyDisbanded : IEvent
{
    public readonly MobileParty SelectedParty;

    public ClanPartyDisbanded(MobileParty selectedParty)
    {
        SelectedParty = selectedParty;
    }
}