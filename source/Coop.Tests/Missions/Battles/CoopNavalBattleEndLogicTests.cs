#if DEBUG
using HarmonyLib;
using Missions.Naval;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopNavalBattleEndLogicTests
{
    [Theory]
    [InlineData(BattleState.AttackerVictory, BattleSideEnum.Attacker, NavalBattleOutcome.PlayerVictory)]
    [InlineData(BattleState.AttackerVictory, BattleSideEnum.Defender, NavalBattleOutcome.PlayerDefeat)]
    [InlineData(BattleState.DefenderVictory, BattleSideEnum.Defender, NavalBattleOutcome.PlayerVictory)]
    [InlineData(BattleState.DefenderVictory, BattleSideEnum.Attacker, NavalBattleOutcome.PlayerDefeat)]
    [InlineData(BattleState.DefenderPullBack, BattleSideEnum.Attacker, NavalBattleOutcome.None)]
    [InlineData(BattleState.None, BattleSideEnum.Defender, NavalBattleOutcome.None)]
    [InlineData(BattleState.AttackerVictory, BattleSideEnum.None, NavalBattleOutcome.None)]
    public void OutcomeOf_MapsTheSharedResultOntoTheLocalPlayersSide(BattleState state, BattleSideEnum playerSide, NavalBattleOutcome expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.OutcomeOf(state, playerSide));
    }

    [Fact]
    public void ReplicatedOutcome_LeavesTheHostToItsOwnDetectionUntilAResultIsResolved()
    {
        Assert.Null(CoopNavalBattleEndLogic.ReplicatedOutcome(
            isLocalHost: true, deploymentFinished: true, hasResolvedState: false, BattleState.None, BattleSideEnum.Attacker));
    }

    [Fact]
    public void ReplicatedOutcome_KeepsAPeerRunningWithoutTheHostsResult()
    {
        Assert.Equal(NavalBattleOutcome.None, CoopNavalBattleEndLogic.ReplicatedOutcome(
            isLocalHost: false, deploymentFinished: true, hasResolvedState: false, BattleState.None, BattleSideEnum.Attacker));
    }

    [Fact]
    public void ReplicatedOutcome_HoldsTheHostsResultUntilThePeersDeploymentFinished()
    {
        Assert.Equal(NavalBattleOutcome.None, CoopNavalBattleEndLogic.ReplicatedOutcome(
            isLocalHost: false, deploymentFinished: false, hasResolvedState: true, BattleState.AttackerVictory, BattleSideEnum.Attacker));
    }

    [Theory]
    [InlineData(false, BattleState.AttackerVictory, BattleSideEnum.Attacker, NavalBattleOutcome.PlayerVictory)]
    [InlineData(false, BattleState.AttackerVictory, BattleSideEnum.Defender, NavalBattleOutcome.PlayerDefeat)]
    [InlineData(true, BattleState.DefenderVictory, BattleSideEnum.Attacker, NavalBattleOutcome.PlayerDefeat)]
    public void ReplicatedOutcome_EndsEveryClientWithTheResolvedResult(bool isLocalHost, BattleState state, BattleSideEnum playerSide,
        NavalBattleOutcome expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.ReplicatedOutcome(isLocalHost, deploymentFinished: true, hasResolvedState: true,
            state, playerSide));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, NavalBattleOutcome.None)]
    public void ReplicatedOutcome_IgnoresAResolvedStateWithNoVictor(bool isLocalHost, NavalBattleOutcome? expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.ReplicatedOutcome(isLocalHost, deploymentFinished: true, hasResolvedState: true,
            BattleState.DefenderPullBack, BattleSideEnum.Defender));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldCheckEnd_OnlyOnTheHostOnceBothSidesFielded(bool isLocalHost, bool released, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.ShouldCheckEnd(isLocalHost, released));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 5, false)]
    [InlineData(1, 0, false)]
    public void IsSideDepleted_NeedsNoLiveTroopAndNoFieldableReserve(int liveHumans, int reserve, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.IsSideDepleted(liveHumans, reserve));
    }

    [Theory]
    [InlineData(true, 20, 30, true)]
    [InlineData(false, 3, 0, true)]
    [InlineData(false, 1, 2, true)]
    [InlineData(false, 2, 2, false)]
    [InlineData(false, 4, 0, false)]
    [InlineData(false, 0, 0, true)]
    public void IsHullOutOfAction_FollowsVanillasSunkOrThreeTroopsRule(bool isSunk, int liveCrew, int reserve, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.IsHullOutOfAction(isSunk, liveCrew, reserve));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    public void IsSideOutOfAction_NeedsEveryHullOutOfAction(int hulls, int hullsOutOfAction, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.IsSideOutOfAction(hulls, hullsOutOfAction));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void CountsReserve_OnlyOnAnAfloatHullSimulatedHere(bool simulatedHere, bool isSunk, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.CountsReserve(simulatedHere, isSunk));
    }

    [Fact]
    public void DescribeResult_NamesVictoryDefeatAndNone()
    {
        Assert.Equal("victory", CoopNavalBattleEndLogic.DescribeResult(new MissionResult(BattleState.AttackerVictory, true, false, false)));
        Assert.Equal("defeat", CoopNavalBattleEndLogic.DescribeResult(new MissionResult(BattleState.AttackerVictory, false, true, false)));
        Assert.Equal("none", CoopNavalBattleEndLogic.DescribeResult(new MissionResult()));
        Assert.Equal("none", CoopNavalBattleEndLogic.DescribeResult(null));
    }

    [Fact]
    public void EndPatches_BindToTheVanillaDepletionOutOfActionAndShipCaptureMethods()
    {
        var harmony = new Harmony("coop.tests.naval.end_patches");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CoopNavalSideDepletionPatch)).Patch());
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CoopNavalHullsOutOfActionPatch)).Patch());
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CoopNavalShipCapturePatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
#endif
