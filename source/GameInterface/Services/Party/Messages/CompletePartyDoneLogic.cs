using Common.Messaging;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.Party.Data;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompletePartyDoneLogic : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly FlattenedTroop[] TakenPrisonersRoster;

    [ProtoMember(3)]
    public readonly FlattenedTroop[] DonatedPrisonersRoster;

    [ProtoMember(4)]
    public readonly FlattenedTroop[] RecruitedPrisonersRoster;

    [ProtoMember(5)]
    public readonly TroopRosterData LeftMemberRosterData;

    [ProtoMember(6)]
    public readonly TroopRosterData LeftPrisonerRosterData;

    [ProtoMember(7)]
    public readonly TroopRosterData RightMemberRosterData;

    [ProtoMember(8)]
    public readonly TroopRosterData RightPrisonerRosterData;

    [ProtoMember(9)]
    public readonly ItemRosterElement[] RightOwnerPartyItemRosterData;

    [ProtoMember(10)]
    public readonly UpgradedTroopHistoryData UpgradedTroopHistoryIds;

    [ProtoMember(11)]
    public readonly string LeftPartyId;

    [ProtoMember(12)]
    public readonly string LeftPrisonerRosterId;

    [ProtoMember(13)]
    public readonly int PartyGoldChangeAmount;

    [ProtoMember(14)]
    public readonly int PartyInfluenceChangeAmount;

    [ProtoMember(15)]
    public readonly int PartyMoraleChangeAmount;

    [ProtoMember(16)]
    public readonly bool DoNotApplyGoldTransactions;

    public CompletePartyDoneLogic(
        string mainHeroId,
        FlattenedTroop[] takenPrisonersRoster,
        FlattenedTroop[] donatedPrisonersRoster,
        FlattenedTroop[] recruitedPrisonersRoster,
        TroopRosterData leftMemberRosterData,
        TroopRosterData leftPrisonerRosterData,
        TroopRosterData rightMemberRosterData,
        TroopRosterData rightPrisonerRosterData,
        ItemRosterElement[] rightOwnerPartyItemRosterData,
        UpgradedTroopHistoryData upgradedTroopHistoryIds,
        string leftPartyId,
        string leftPrisonerRosterId,
        int partyGoldChangeAmount,
        int partyInfluenceChangeAmount,
        int partyMoraleChangeAmount,
        bool doNotApplyGoldTransactions)
    {
        MainHeroId = mainHeroId;
        TakenPrisonersRoster = takenPrisonersRoster;
        DonatedPrisonersRoster = donatedPrisonersRoster;
        RecruitedPrisonersRoster = recruitedPrisonersRoster;
        LeftMemberRosterData = leftMemberRosterData;
        LeftPrisonerRosterData = leftPrisonerRosterData;
        RightMemberRosterData = rightMemberRosterData;
        RightPrisonerRosterData = rightPrisonerRosterData;
        RightOwnerPartyItemRosterData = rightOwnerPartyItemRosterData;
        UpgradedTroopHistoryIds = upgradedTroopHistoryIds;
        LeftPartyId = leftPartyId;
        LeftPrisonerRosterId = leftPrisonerRosterId;
        PartyGoldChangeAmount = partyGoldChangeAmount;
        PartyInfluenceChangeAmount = partyInfluenceChangeAmount;
        PartyMoraleChangeAmount = partyMoraleChangeAmount;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
    }
}