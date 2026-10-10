using GameInterface.Services.Party.Handlers;
using GameInterface.Services.Party.Patches;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Party;

public class PartyScreenPrisonerReleaseTests
{
    [Fact]
    public void ReleaseActions_AreNotRequestedForEmptyPrisonerRosters()
    {
        // Vanilla DefaultDoneHandler calls HandleReleasedAndTakenPrisoners
        // unconditionally, so empty rosters (e.g. a force volunteers loot
        // screen with no prisoner moves) must not flag prisoner actions.
        PartyScreenHelperPatches.ResetReleasedAndTakenPrisonerActionsRequest();

        Assert.False(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());

        PartyScreenHelperPatches.HandleReleasedAndTakenPrisonersPrefix(
            new FlattenedTroopRoster(4),
            new FlattenedTroopRoster(4));

        Assert.False(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());
    }

    [Fact]
    public void ReleaseActions_AreRequestedForNonEmptyPrisonerRosters()
    {
        PartyScreenHelperPatches.ResetReleasedAndTakenPrisonerActionsRequest();

        var taken = new FlattenedTroopRoster(4);
        var descriptor = new UniqueTroopDescriptor(123);
        taken[descriptor] = new FlattenedTroopRosterElement(null, RosterTroopState.Active, 0, descriptor);

        PartyScreenHelperPatches.HandleReleasedAndTakenPrisonersPrefix(
            taken,
            new FlattenedTroopRoster(4));

        Assert.True(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());
        Assert.False(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());
    }

    [Fact]
    public void TakenHeroAdditions_AreOwnedByTakePrisonerAction()
    {
        var delta = new GameInterface.Services.TroopRosters.Data.TroopRosterData(new[]
        {
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData(1, 1, 0, 0),
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData(2, 3, 0, 0),
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData(3, -1, 0, 0),
        });

        var filtered = PartyDoneLogicHandler.FilterTakenHeroAdditions(
            delta,
            new HashSet<uint> { 1, 3 });

        Assert.Collection(
            filtered.Data,
            element => Assert.Equal(2u, element.CharacterId),
            element => Assert.Equal(3u, element.CharacterId));
    }

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void DefaultReleaseCallback_OverridesLeftTransferDestination(
        bool applyReleaseActions,
        bool hasLeftParty,
        bool hasLeftPrisonerRoster,
        bool expected)
    {
        Assert.Equal(
            expected,
            PartyDoneLogicHandler.HasLeftPrisonerTransferDestination(
                applyReleaseActions,
                hasLeftParty,
                hasLeftPrisonerRoster));
    }

    [Fact]
    public void CommitRollback_RestoresBothLeftRosterSlots()
    {
        var logic = new PartyScreenLogic();
        var originalRightMember = TroopRoster.CreateDummyTroopRoster();
        var originalRightPrisoner = TroopRoster.CreateDummyTroopRoster();
        var leftMember = TroopRoster.CreateDummyTroopRoster();
        var leftPrisoner = TroopRoster.CreateDummyTroopRoster();
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = originalRightMember;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = originalRightPrisoner;

        PartyScreenLogicPatches.RestoreLeftRostersAfterCommit(logic, leftMember, leftPrisoner);

        Assert.Same(leftMember, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left]);
        Assert.Same(leftPrisoner, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left]);
        Assert.Same(originalRightMember, logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]);
        Assert.Same(originalRightPrisoner, logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right]);
    }
}
