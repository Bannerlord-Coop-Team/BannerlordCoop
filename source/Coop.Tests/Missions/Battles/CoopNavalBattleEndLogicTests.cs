#if DEBUG
using HarmonyLib;
using Missions.Battles;
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

    // M8a: vanilla reads a side with no hull as retreated; in coop that stands only once that side's hulls left with their
    // owners and none is left, never before its hulls spawn.
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(2, 0, true)]
    public void KeepsRetreat_OnlyOnceEveryHullLeftWithItsOwner(int departedHulls, int hullsLeft, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleEndLogic.KeepsRetreat(departedHulls, hullsLeft));
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

    [Fact]
    public void SideTally_UsesVanillasOutOfActionTroopCount()
    {
        Assert.Equal(TaleWorlds.MountAndBlade.NavalBattleEndLogic.MinTroopCountForOutOfActionCheck, NavalSideTally.MinTroopCountForOutOfAction);
    }

    [Fact]
    public void HeroGrace_UsesVanillasMainAgentConsideredDeadDuration()
    {
        Assert.Equal(TaleWorlds.MountAndBlade.NavalBattleEndLogic.MainAgentConsideredDeadDuration,
            NavalPlayerHeroGrace.HeroConsideredDeadSeconds);
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
