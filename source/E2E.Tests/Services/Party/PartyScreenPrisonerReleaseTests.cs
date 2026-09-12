using GameInterface.Services.Party.Handlers;
using GameInterface.Services.Party.Patches;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace E2E.Tests.Services.Party;

public class PartyScreenPrisonerReleaseTests
{
    [Fact]
    public void ReleaseActions_AreRequestedOnlyByVanillaDefaultDoneCallback()
    {
        PartyScreenHelperPatches.ResetReleasedAndTakenPrisonerActionsRequest();

        Assert.False(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());

        PartyScreenHelperPatches.HandleReleasedAndTakenPrisonersPrefix(
            new FlattenedTroopRoster(4),
            new FlattenedTroopRoster(4));

        Assert.True(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());
        Assert.False(PartyScreenHelperPatches.ConsumeReleasedAndTakenPrisonerActionsRequest());
    }

    [Fact]
    public void TakenHeroAdditions_AreOwnedByTakePrisonerAction()
    {
        var delta = new GameInterface.Services.TroopRosters.Data.TroopRosterData(new[]
        {
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData("taken-hero", 1, 0, 0),
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData("regular-troop", 3, 0, 0),
            new GameInterface.Services.TroopRosters.Data.TroopRosterElementData("source-removal", -1, 0, 0),
        });

        var filtered = PartyDoneLogicHandler.FilterTakenHeroAdditions(
            delta,
            new HashSet<string> { "taken-hero", "source-removal" });

        Assert.Collection(
            filtered.Data,
            element => Assert.Equal("regular-troop", element.CharacterId),
            element => Assert.Equal("source-removal", element.CharacterId));
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
