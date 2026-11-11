using GameInterface.Services.Party.Patches;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace E2E.Tests.Services.Party;

public class PartyScreenCommitRollbackTests
{
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
