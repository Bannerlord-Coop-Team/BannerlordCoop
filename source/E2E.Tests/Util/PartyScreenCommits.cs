using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using Helpers;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util;

/// <summary>
/// Builds party screen commits for tests, defaulting every field a test doesn't set.
/// </summary>
internal static class PartyScreenCommits
{
    public static NetworkCommitPartyScreen Create(
        string mainHeroId,
        PartyScreenHelper.PartyScreenMode mode,
        PartyScreenDoneKind doneKind,
        PartyScreenCommandData[] commands,
        TroopRosterOrderData rightMemberOrder = null,
        string leftPartyId = null,
        TroopRosterData leftMemberSeed = default,
        TroopRosterData leftPrisonerSeed = default,
        bool isDonating = false,
        bool doNotApplyGoldTransactions = true,
        string forceTransferId = null,
        string issueOwnerId = null,
        string issueId = null)
        => new(
            mainHeroId,
            mode,
            doneKind,
            commands,
            rightMemberOrder ?? new TroopRosterOrderData(new()),
            leftPartyId,
            leftMemberSeed,
            leftPrisonerSeed,
            isDonating,
            doNotApplyGoldTransactions,
            forceTransferId,
            issueOwnerId,
            issueId);

    public static PartyScreenCommandData Transfer(
        PartyScreenLogic.PartyRosterSide from,
        PartyScreenLogic.TroopType type,
        uint characterHandle,
        int number,
        int wounded = 0)
        => new(PartyScreenLogic.PartyCommandCode.TransferTroop, from, type, characterHandle, number, wounded, 0);

    public static PartyScreenCommandData Upgrade(uint characterHandle, int number, int upgradeTarget = 0)
        => new(
            PartyScreenLogic.PartyCommandCode.UpgradeTroop,
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member,
            characterHandle,
            number,
            0,
            upgradeTarget);

    public static PartyScreenCommandData Recruit(uint characterHandle, int number)
        => new(
            PartyScreenLogic.PartyCommandCode.RecruitTroop,
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Prisoner,
            characterHandle,
            number,
            0,
            0);
}
