using GameInterface.Services.Party;
using GameInterface.Services.Party.Patches;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PartyScreenRosterRebaserTests
{
    [Fact]
    public void TryRebase_AuthoritativeWoundedChangeWithNoLocalEdit_UpdatesVisibleState()
    {
        var baseline = new RosterElementState(1, 1, 0);
        var authoritative = new RosterElementState(1, 0, 0);

        var result = RosterElementState.TryRebase(
            baseline,
            baseline,
            authoritative,
            authoritative.Exists,
            out var rebased);

        Assert.True(result);
        Assert.Equal(1, rebased.Number);
        Assert.Equal(0, rebased.Wounded);
        Assert.Equal(0, rebased.Xp);
    }

    [Fact]
    public void TryRebase_DetachedCloneWoundedChange_DoesNotInjectOwnerXp()
    {
        var cloneBaseline = new RosterElementState(3, 1, 0);
        var cloneAfter = new RosterElementState(3, 0, 0);

        var result = RosterElementState.TryRebase(
            cloneBaseline,
            cloneBaseline,
            cloneAfter,
            cloneAfter.Exists,
            out var rebased);

        Assert.True(result);
        Assert.Equal(3, rebased.Number);
        Assert.Equal(0, rebased.Wounded);
        Assert.Equal(0, rebased.Xp);
    }

    [Fact]
    public void SeedOmittedZeroRow_DetachedCloneCanApplyAbsoluteRevival()
    {
        var retainedOwnerRow = new RosterElementState(0, 0, 50);
        var cloneBaseline = RosterElementState.SeedOmittedZeroRow(
            default,
            retainedOwnerRow,
            copyXp: false);
        var character = new CharacterObject();
        var scratch = TroopRoster.CreateDummyTroopRoster();
        RosterElementState.Write(scratch, character, cloneBaseline);
        scratch.SetElementNumber(scratch.FindIndexOfTroop(character), 2);
        var cloneAfter = RosterElementState.Read(scratch, character);

        var result = RosterElementState.TryRebase(
            cloneBaseline,
            cloneBaseline,
            cloneAfter,
            cloneAfter.Exists,
            out var rebased);

        Assert.True(cloneBaseline.Exists);
        Assert.Equal(0, cloneBaseline.Xp);
        Assert.True(result);
        Assert.Equal(2, rebased.Number);
        Assert.Equal(0, rebased.Xp);
    }

    [Fact]
    public void SeedOmittedZeroRow_AliasedBaselineKeepsCanonicalXp()
    {
        var retainedOwnerRow = new RosterElementState(0, 0, 50);

        var baseline = RosterElementState.SeedOmittedZeroRow(
            default,
            retainedOwnerRow,
            copyXp: true);

        Assert.True(baseline.Exists);
        Assert.Equal(50, baseline.Xp);
    }

    [Fact]
    public void TryRebase_CompatibleLocalEdit_PreservesItsDeltaOverAuthoritativeState()
    {
        var baseline = new RosterElementState(5, 1, 100);
        var visible = new RosterElementState(7, 1, 140);
        var authoritative = new RosterElementState(6, 2, 120);

        var result = RosterElementState.TryRebase(
            baseline,
            visible,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: true,
            out var rebased);

        Assert.True(result);
        Assert.Equal(8, rebased.Number);
        Assert.Equal(2, rebased.Wounded);
        Assert.Equal(160, rebased.Xp);
    }

    [Fact]
    public void TryRebase_ConcurrentRemovalConflictsWithLocalRemoval_ReturnsFalse()
    {
        var baseline = new RosterElementState(1, 0, 0);
        var visible = new RosterElementState(0, 0, 0);
        var authoritative = new RosterElementState(0, 0, 0);

        var result = RosterElementState.TryRebase(
            baseline,
            visible,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: false,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryRebase_ResultingWoundedCountAboveTotal_ReturnsFalse()
    {
        var baseline = new RosterElementState(2, 0, 0);
        var visible = new RosterElementState(2, 2, 0);
        var authoritative = new RosterElementState(1, 0, 0);

        var result = RosterElementState.TryRebase(
            baseline,
            visible,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: true,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryRebase_AuthoritativeElementAddition_AddsItToVisibleState()
    {
        var authoritative = new RosterElementState(3, 1, 25);

        var result = RosterElementState.TryRebase(
            default,
            default,
            authoritative,
            authoritative.Exists,
            out var rebased);

        Assert.True(result);
        Assert.Equal(3, rebased.Number);
        Assert.Equal(1, rebased.Wounded);
        Assert.Equal(25, rebased.Xp);
    }

    [Fact]
    public void TryRebase_AuthoritativeElementRemoval_RemovesItFromVisibleState()
    {
        var baseline = new RosterElementState(2, 1, 30);

        var result = RosterElementState.TryRebase(
            baseline,
            baseline,
            default,
            existsAfterApplyingAuthoritativeToVisible: false,
            out var rebased);

        Assert.True(result);
        Assert.False(rebased.Exists);
        Assert.Equal(0, rebased.Number);
        Assert.Equal(0, rebased.Wounded);
        Assert.Equal(0, rebased.Xp);
    }

    [Fact]
    public void TryRebase_SequentialUpdatesUseLatestAuthoritativeBaseline()
    {
        var screenOpenBaseline = new RosterElementState(5, 1, 100);
        var firstAuthoritative = new RosterElementState(6, 2, 120);

        Assert.True(RosterElementState.TryRebase(
            screenOpenBaseline,
            screenOpenBaseline,
            firstAuthoritative,
            firstAuthoritative.Exists,
            out var firstVisible));

        var visibleWithLocalEdit = new RosterElementState(7, 2, 140);
        var secondAuthoritative = new RosterElementState(8, 1, 160);
        Assert.True(RosterElementState.TryRebase(
            firstAuthoritative,
            visibleWithLocalEdit,
            secondAuthoritative,
            secondAuthoritative.Exists,
            out var secondVisible));

        Assert.Equal(9, secondVisible.Number);
        Assert.Equal(1, secondVisible.Wounded);
        Assert.Equal(180, secondVisible.Xp);
        Assert.Equal(6, firstVisible.Number);
    }

    [Fact]
    public void TryRebase_AuthoritativeZeroCountWithRetainedXp_IsValid()
    {
        var baseline = new RosterElementState(1, 0, 50);
        var authoritative = new RosterElementState(0, 0, 50);

        var result = RosterElementState.TryRebase(
            baseline,
            baseline,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: true,
            out var rebased);

        Assert.True(result);
        Assert.True(rebased.Exists);
        Assert.Equal(0, rebased.Number);
        Assert.Equal(0, rebased.Wounded);
        Assert.Equal(50, rebased.Xp);
    }

    [Fact]
    public void TryRebase_AddCountsRemovingLastLocalTroop_DoesNotRecreateZeroRow()
    {
        var baseline = new RosterElementState(2, 0, 0);
        var visible = new RosterElementState(1, 0, 0);
        var authoritative = new RosterElementState(1, 0, 0);

        var result = RosterElementState.TryRebase(
            baseline,
            visible,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: false,
            out var rebased);

        Assert.True(result);
        Assert.False(rebased.Exists);
        Assert.Equal(0, rebased.Number);
    }

    [Fact]
    public void TryRebase_SetNumberToZero_RetainsZeroRow()
    {
        var baseline = new RosterElementState(1, 0, 0);
        var authoritative = new RosterElementState(0, 0, 0);

        var result = RosterElementState.TryRebase(
            baseline,
            baseline,
            authoritative,
            existsAfterApplyingAuthoritativeToVisible: true,
            out var rebased);

        Assert.True(result);
        Assert.True(rebased.Exists);
        Assert.Equal(0, rebased.Number);
    }

    [Fact]
    public void Write_ZeroCountWithRetainedXp_PreservesElementUntilRemoveZeroCounts()
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();

        RosterElementState.Write(roster, character, new RosterElementState(0, 0, 50));

        Assert.Equal(0, roster.FindIndexOfTroop(character));
        Assert.Equal(50, roster.GetElementXp(0));

        roster.RemoveZeroCounts();

        Assert.Equal(-1, roster.FindIndexOfTroop(character));
    }

    [Fact]
    public void Write_ChangesPresenceWhenNumericFieldsAreEqual()
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        var retainedZero = new RosterElementState(0, 0, 0);

        RosterElementState.Write(roster, character, retainedZero);
        Assert.Equal(0, roster.FindIndexOfTroop(character));

        RosterElementState.Write(roster, character, default);
        Assert.Equal(-1, roster.FindIndexOfTroop(character));

        RosterElementState.Write(roster, character, retainedZero);
        Assert.Equal(0, roster.FindIndexOfTroop(character));
    }

    [Fact]
    public void GuiZeroRowCleanup_PreservesAliasedOwnerAndSeedsBaseline()
    {
        var owner = TroopRoster.CreateDummyTroopRoster();
        var baseline = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        RosterElementState.Write(owner, character, new RosterElementState(0, 0, 50));
        var preservedRows = new List<PartyScreenLogicPatches.PreservedZeroRow>();
        PartyScreenLogicPatches.CaptureOwnerZeroRows(
            owner,
            baseline,
            owner,
            preserveMissingBaseline: true,
            preservedRows);

        owner.RemoveZeroCounts();
        PartyScreenLogicPatches.RemoveZeroCountsPostfix(preservedRows);

        var restoredOwner = RosterElementState.Read(owner, character);
        var restoredBaseline = RosterElementState.Read(baseline, character);
        Assert.True(restoredOwner.Exists);
        Assert.Equal(50, restoredOwner.Xp);
        Assert.True(restoredBaseline.Exists);
        Assert.Equal(50, restoredBaseline.Xp);
    }

    [Fact]
    public void GuiZeroRowCleanup_LocalRemovalFromPositiveBaseline_IsNotPreserved()
    {
        var owner = TroopRoster.CreateDummyTroopRoster();
        var baseline = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        RosterElementState.Write(owner, character, new RosterElementState(0, 0, 0));
        RosterElementState.Write(baseline, character, new RosterElementState(3, 0, 0));
        var preservedRows = new List<PartyScreenLogicPatches.PreservedZeroRow>();

        PartyScreenLogicPatches.CaptureOwnerZeroRows(
            owner,
            baseline,
            owner,
            preserveMissingBaseline: false,
            preservedRows);

        Assert.Empty(preservedRows);
    }

    [Fact]
    public void GuiZeroRowCleanup_LocalRoundTripMissingFromBaseline_IsNotPreserved()
    {
        var owner = TroopRoster.CreateDummyTroopRoster();
        var baseline = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        RosterElementState.Write(owner, character, new RosterElementState(0, 0, 50));
        var preservedRows = new List<PartyScreenLogicPatches.PreservedZeroRow>();

        PartyScreenLogicPatches.CaptureOwnerZeroRows(
            owner,
            baseline,
            owner,
            preserveMissingBaseline: false,
            preservedRows);

        Assert.Empty(preservedRows);
    }

    [Fact]
    public void GuiZeroRowCleanup_RetainedBaselineZero_IsPreservedAfterInitialization()
    {
        var owner = TroopRoster.CreateDummyTroopRoster();
        var baseline = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        var retainedZero = new RosterElementState(0, 0, 50);
        RosterElementState.Write(owner, character, retainedZero);
        RosterElementState.Write(baseline, character, retainedZero);
        var preservedRows = new List<PartyScreenLogicPatches.PreservedZeroRow>();

        PartyScreenLogicPatches.CaptureOwnerZeroRows(
            owner,
            baseline,
            owner,
            preserveMissingBaseline: false,
            preservedRows);

        Assert.Single(preservedRows);
        Assert.False(preservedRows[0].SeedBaseline);
    }

    [Fact]
    public void PartyVMInitializationMarker_TracksOnlyTheConstructingLogic()
    {
        var constructingLogic = new PartyScreenLogic();
        var otherLogic = new PartyScreenLogic();

        PartyVMInitializationPatches.Prefix(constructingLogic, out var previous);
        try
        {
            Assert.True(PartyVMInitializationPatches.IsInitializing(constructingLogic));
            Assert.False(PartyVMInitializationPatches.IsInitializing(otherLogic));
        }
        finally
        {
            PartyVMInitializationPatches.Finalizer(previous, null);
        }

        Assert.False(PartyVMInitializationPatches.IsInitializing(constructingLogic));
    }

    [Fact]
    public void PartyScreenZeroRows_RestoreAllRostersAfterVanillaRebuildDropsThem()
    {
        var source = CreatePartyScreenData();
        var leftMember = new CharacterObject();
        var leftPrisoner = new CharacterObject();
        var rightMember = new CharacterObject();
        var rightPrisoner = new CharacterObject();
        RosterElementState.Write(
            source.LeftMemberRoster,
            leftMember,
            new RosterElementState(0, 0, 10));
        RosterElementState.Write(
            source.LeftPrisonerRoster,
            leftPrisoner,
            new RosterElementState(0, 0, 20));
        RosterElementState.Write(
            source.RightMemberRoster,
            rightMember,
            new RosterElementState(0, 0, 30));
        RosterElementState.Write(
            source.RightPrisonerRoster,
            rightPrisoner,
            new RosterElementState(0, 0, 40));
        var zeroRows = PartyScreenZeroRows.Capture(source);
        var copied = CreatePartyScreenData();
        var reset = CreatePartyScreenData();

        copied.CopyFromScreenData(source);
        reset.ResetUsing(source);

        Assert.Equal(-1, copied.LeftMemberRoster.FindIndexOfTroop(leftMember));
        Assert.Equal(-1, reset.RightPrisonerRoster.FindIndexOfTroop(rightPrisoner));

        zeroRows.Restore(copied);
        zeroRows.Restore(reset);

        Assert.Equal(10, copied.LeftMemberRoster.GetElementXp(
            copied.LeftMemberRoster.FindIndexOfTroop(leftMember)));
        Assert.Equal(20, copied.LeftPrisonerRoster.GetElementXp(
            copied.LeftPrisonerRoster.FindIndexOfTroop(leftPrisoner)));
        Assert.Equal(30, copied.RightMemberRoster.GetElementXp(
            copied.RightMemberRoster.FindIndexOfTroop(rightMember)));
        Assert.Equal(40, copied.RightPrisonerRoster.GetElementXp(
            copied.RightPrisonerRoster.FindIndexOfTroop(rightPrisoner)));
        Assert.Equal(10, reset.LeftMemberRoster.GetElementXp(
            reset.LeftMemberRoster.FindIndexOfTroop(leftMember)));
        Assert.Equal(20, reset.LeftPrisonerRoster.GetElementXp(
            reset.LeftPrisonerRoster.FindIndexOfTroop(leftPrisoner)));
        Assert.Equal(30, reset.RightMemberRoster.GetElementXp(
            reset.RightMemberRoster.FindIndexOfTroop(rightMember)));
        Assert.Equal(40, reset.RightPrisonerRoster.GetElementXp(
            reset.RightPrisonerRoster.FindIndexOfTroop(rightPrisoner)));
    }

    private static PartyScreenData CreatePartyScreenData()
    {
        var data = new PartyScreenData();
        data.InitializeCopyFrom(null, null);
        return data;
    }

    [Fact]
    public void Write_DirectSetRecalculatesRosterTotals()
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();

        RosterElementState.Write(roster, character, new RosterElementState(5, 2, 30));
        RosterElementState.Write(roster, character, new RosterElementState(3, 1, 10));

        Assert.Equal(3, roster.TotalManCount);
        Assert.Equal(1, roster.TotalWounded);
        Assert.Equal(2, roster.TotalHealthyCount);
    }

    [Fact]
    public void RostersMatchOwnerSnapshot_VanillaCloneWithoutXpOrZeroRows_ReturnsTrue()
    {
        var owner = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        var retainedZeroCharacter = new CharacterObject();
        RosterElementState.Write(owner, character, new RosterElementState(3, 1, 20));
        RosterElementState.Write(
            owner,
            retainedZeroCharacter,
            new RosterElementState(0, 0, 50));
        var snapshot = owner.CloneRosterData();

        Assert.Equal(2, owner.Count);
        Assert.Single(snapshot.GetTroopRoster());
        Assert.Equal(0, snapshot.GetElementXp(0));
        Assert.Equal(-1, snapshot.FindIndexOfTroop(retainedZeroCharacter));
        Assert.True(RosterElementState.RostersMatchOwnerSnapshot(snapshot, owner));
    }

    [Fact]
    public void RostersMatchOwnerSnapshot_SequentialXpDeltaStillReturnsTrue()
    {
        var snapshot = TroopRoster.CreateDummyTroopRoster();
        var owner = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        RosterElementState.Write(snapshot, character, new RosterElementState(3, 1, 10));
        RosterElementState.Write(owner, character, new RosterElementState(3, 1, 60));

        Assert.True(RosterElementState.RostersMatchOwnerSnapshot(snapshot, owner));
    }

    [Fact]
    public void RostersMatchOwnerSnapshot_FilteredOrDummyRoster_ReturnsFalse()
    {
        var filtered = TroopRoster.CreateDummyTroopRoster();
        var authoritative = TroopRoster.CreateDummyTroopRoster();
        var character = new CharacterObject();
        RosterElementState.Write(authoritative, character, new RosterElementState(3, 1, 20));

        Assert.False(RosterElementState.RostersMatchOwnerSnapshot(filtered, authoritative));
    }

    [Fact]
    public void RecruitableFromRosterState_AuthoritativeNewPrisoner_AddsEntry()
    {
        var authoritative = new RosterElementState(3, 0, 20);

        var result = RecruitableState.FromRosterState(
            authoritative,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true);

        Assert.True(result.Exists);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void RecruitableTryRebase_CloneTransferIn_DoesNotGrantTransferredPrisoners()
    {
        var baseline = new RosterElementState(2, 0, 20);
        var rebasedVisible = new RosterElementState(6, 0, 50);
        var authoritative = new RosterElementState(3, 0, 20);
        var current = new RecruitableState(2);
        int capacityBefore = RecruitableState.FromRosterState(
            baseline,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true).Value;
        int capacityAfter = RecruitableState.FromRosterState(
            authoritative,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true).Value;

        var result = RecruitableState.TryRebase(
            current,
            capacityBefore,
            capacityAfter,
            hasKey: true,
            out var rebased);

        Assert.True(result);
        Assert.True(rebased.Exists);
        Assert.Equal(2, rebased.Value);
        Assert.Equal(5, RecruitableState.FromRosterState(
            rebasedVisible,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true).Value);
    }

    [Fact]
    public void RecruitableTryRebase_AliasedTransferOut_PreservesRemainingRecruitablePrisoners()
    {
        var visibleBefore = new RosterElementState(2, 0, 50);
        var rebasedVisible = new RosterElementState(2, 0, 30);
        var current = new RecruitableState(2);
        int capacityBefore = RecruitableState.FromRosterState(
            visibleBefore,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true).Value;
        int capacityAfter = RecruitableState.FromRosterState(
            rebasedVisible,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true).Value;

        var result = RecruitableState.TryRebase(
            current,
            capacityBefore,
            capacityAfter,
            hasKey: true,
            out var rebased);

        Assert.True(result);
        Assert.Equal(2, rebased.Value);
    }

    [Fact]
    public void RecruitableTryRebase_AuthoritativeCapacityChange_AdvancesIndependentValue()
    {
        var current = new RecruitableState(2);

        var result = RecruitableState.TryRebase(
            current,
            capacityBefore: 2,
            capacityAfter: 5,
            hasKey: true,
            out var rebased);

        Assert.True(result);
        Assert.Equal(5, rebased.Value);
    }

    [Fact]
    public void RecruitableTryRebase_OverlappingAuthoritativeDecrease_ReturnsFalse()
    {
        var current = new RecruitableState(1);

        var result = RecruitableState.TryRebase(
            current,
            capacityBefore: 5,
            capacityAfter: 3,
            hasKey: true,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void RecruitableFromRosterState_LocallyTransferredInPrisonerWithoutKey_StaysMissing()
    {
        var rebasedVisible = new RosterElementState(2, 0, 30);

        var result = RecruitableState.FromRosterState(
            rebasedVisible,
            isHero: false,
            conformityNeeded: 10,
            hasKey: false);

        Assert.False(result.Exists);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void RecruitableFromRosterState_ExistingKeySurvivesAbsentRosterRow()
    {
        var result = RecruitableState.FromRosterState(
            default,
            isHero: false,
            conformityNeeded: 10,
            hasKey: true);

        Assert.True(result.Exists);
        Assert.Equal(0, result.Value);
    }
}
