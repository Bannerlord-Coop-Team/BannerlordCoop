using Common.Messaging;
using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Messages;

public readonly struct PartyDoneLogicAttempted : IEvent
{
    public readonly Hero MainHero;
    public readonly FlattenedTroopRoster TakenPrisonersRoster;
    public readonly FlattenedTroopRoster DonatedPrisonersRoster;
    public readonly FlattenedTroopRoster RecruitedPrisonersRoster;
    public readonly TroopRoster LeftMemberRoster;
    public readonly TroopRoster LeftPrisonerRoster;
    public readonly TroopRoster RightMemberRoster;
    public readonly TroopRoster RightPrisonerRoster;
    // Snapshots of the rosters as they were when the party screen opened. Used to compute the per-troop
    // delta the player made on the screen (current minus initial) so the server applies only the changes.
    public readonly TroopRoster InitialLeftMemberRoster;
    public readonly TroopRoster InitialLeftPrisonerRoster;
    public readonly TroopRoster InitialRightMemberRoster;
    public readonly TroopRoster InitialRightPrisonerRoster;
    public readonly ItemRoster RightOwnerPartyItemRoster;
    public readonly List<Tuple<CharacterObject, CharacterObject, int>> UpgradedTroopHistory;
    public readonly PartyBase LeftParty;
    public readonly int PartyGoldChangeAmount;
    public readonly int PartyInfluenceChangeAmount;
    public readonly int PartyMoraleChangeAmount;
    public readonly bool DoNotApplyGoldTransactions;
    public readonly PartyScreenHelper.PartyScreenMode PartyScreenMode;

    public PartyDoneLogicAttempted(
        Hero mainHero,
        FlattenedTroopRoster takenPrisonersRoster,
        FlattenedTroopRoster donatedPrisonersRoster,
        FlattenedTroopRoster recruitedPrisonersRoster,
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonerRoster,
        TroopRoster rightMemberRoster,
        TroopRoster rightPrisonerRoster,
        TroopRoster initialLeftMemberRoster,
        TroopRoster initialLeftPrisonerRoster,
        TroopRoster initialRightMemberRoster,
        TroopRoster initialRightPrisonerRoster,
        ItemRoster rightOwnerPartyItemRoster,
        List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory,
        PartyBase leftParty,
        int partyGoldChangeAmount,
        int partyInfluenceChangeAmount,
        int partyMoraleChangeAmount,
        bool doNotApplyGoldTransactions,
        PartyScreenHelper.PartyScreenMode partyScreenMode)
    {
        MainHero = mainHero;
        TakenPrisonersRoster = takenPrisonersRoster;
        DonatedPrisonersRoster = donatedPrisonersRoster;
        RecruitedPrisonersRoster = recruitedPrisonersRoster;
        LeftMemberRoster = leftMemberRoster;
        LeftPrisonerRoster = leftPrisonerRoster;
        RightMemberRoster = rightMemberRoster;
        RightPrisonerRoster = rightPrisonerRoster;
        InitialLeftMemberRoster = initialLeftMemberRoster;
        InitialLeftPrisonerRoster = initialLeftPrisonerRoster;
        InitialRightMemberRoster = initialRightMemberRoster;
        InitialRightPrisonerRoster = initialRightPrisonerRoster;
        RightOwnerPartyItemRoster = rightOwnerPartyItemRoster;
        UpgradedTroopHistory = upgradedTroopHistory;
        LeftParty = leftParty;
        PartyGoldChangeAmount = partyGoldChangeAmount;
        PartyInfluenceChangeAmount = partyInfluenceChangeAmount;
        PartyMoraleChangeAmount = partyMoraleChangeAmount;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
        PartyScreenMode = partyScreenMode;
    }
}