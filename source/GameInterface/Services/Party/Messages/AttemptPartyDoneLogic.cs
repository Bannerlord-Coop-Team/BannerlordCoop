using Common.Messaging;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Messages;

public readonly struct AttemptPartyDoneLogic : IEvent
{
    public readonly Hero MainHero;
    public readonly FlattenedTroopRoster TakenPrisonersRoster;
    public readonly FlattenedTroopRoster DonatedPrisonersRoster;
    public readonly FlattenedTroopRoster RecruitedPrisonersRoster;
    public readonly TroopRoster LeftMemberRoster;
    public readonly TroopRoster LeftPrisonerRoster;
    public readonly TroopRoster RightMemberRoster;
    public readonly TroopRoster RightPrisonerRoster;
    public readonly ItemRoster RightOwnerPartyItemRoster;
    public readonly List<Tuple<CharacterObject, CharacterObject, int>> UpgradedTroopHistory;
    public readonly PartyBase LeftParty;
    public readonly int PartyGoldChangeAmount;
    public readonly int PartyInfluenceChangeAmount;
    public readonly int PartyMoraleChangeAmount;
    public readonly bool DoNotApplyGoldTransactions;

    public AttemptPartyDoneLogic(
        Hero mainHero,
        FlattenedTroopRoster takenPrisonersRoster,
        FlattenedTroopRoster donatedPrisonersRoster,
        FlattenedTroopRoster recruitedPrisonersRoster,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonerRoster,
        TroopRoster rightMemberRoster,
        TroopRoster rightPrisonerRoster,
        ItemRoster rightOwnerPartyItemRoster,
        List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory,
        PartyBase leftParty,
        int partyGoldChangeAmount,
        int partyInfluenceChangeAmount,
        int partyMoraleChangeAmount,
        bool doNotApplyGoldTransactions)
    {
        MainHero = mainHero;
        TakenPrisonersRoster = takenPrisonersRoster;
        DonatedPrisonersRoster = donatedPrisonersRoster;
        RecruitedPrisonersRoster = recruitedPrisonersRoster;
        LeftMemberRoster = leftMemberRoster;
        LeftPrisonerRoster = leftPrisonerRoster;
        RightMemberRoster = rightMemberRoster;
        RightPrisonerRoster = rightPrisonerRoster;
        RightOwnerPartyItemRoster = rightOwnerPartyItemRoster;
        UpgradedTroopHistory = upgradedTroopHistory;
        LeftParty = leftParty;
        PartyGoldChangeAmount = partyGoldChangeAmount;
        PartyInfluenceChangeAmount = partyInfluenceChangeAmount;
        PartyMoraleChangeAmount = partyMoraleChangeAmount;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
    }
}