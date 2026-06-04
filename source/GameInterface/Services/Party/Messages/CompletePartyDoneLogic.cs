using Common.Messaging;
using GameInterface.Services.MapEventParties;
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
    public readonly List<(string, int, int, int)> LeftMemberRosterData;

    [ProtoMember(6)]
    public readonly List<(string, int, int, int)> LeftPrisonerRosterData;

    [ProtoMember(7)]
    public readonly List<(string, int, int, int)> RightMemberRosterData;

    [ProtoMember(8)]
    public readonly List<(string, int, int, int)> RightPrisonerRosterData;

    [ProtoMember(9)]
    public readonly ItemRosterElement[] RightOwnerPartyItemRosterData;

    [ProtoMember(10)]
    public readonly List<Tuple<string, string, int>> UpgradedTroopHistoryIds;

    [ProtoMember(11)]
    public readonly string LeftPartyId;

    [ProtoMember(12)]
    public readonly int PartyGoldChangeAmount;

    [ProtoMember(13)]
    public readonly int PartyInfluenceChangeAmount;

    [ProtoMember(14)]
    public readonly int PartyMoraleChangeAmount;

    [ProtoMember(15)]
    public readonly bool DoNotApplyGoldTransactions;

    public CompletePartyDoneLogic(
        string mainHeroId,
        FlattenedTroop[] takenPrisonersRoster,
        FlattenedTroop[] donatedPrisonersRoster,
        FlattenedTroop[] recruitedPrisonersRoster,
        List<(string, int, int, int)> leftMemberRosterData,
        List<(string, int, int, int)> leftPrisonerRosterData,
        List<(string, int, int, int)> rightMemberRosterData,
        List<(string, int, int, int)> rightPrisonerRosterData,
        ItemRosterElement[] rightOwnerPartyItemRosterData,
        List<Tuple<string, string, int>> upgradedTroopHistoryIds,
        string leftPartyId,
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
        PartyGoldChangeAmount = partyGoldChangeAmount;
        PartyInfluenceChangeAmount = partyInfluenceChangeAmount;
        PartyMoraleChangeAmount = partyMoraleChangeAmount;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
    }
}