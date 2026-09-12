using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Battles;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class SiegeLadderAuthorityPatchTests
{
    [Fact]
    public void SiegeLadderAuthorityPatches_HookVanillaStateMethods()
    {
        var patchType = typeof(BattleSpawnGate).Assembly
            .GetType("GameInterface.Services.MapEvents.Patches.SiegeLadderAuthorityPatches");
        Assert.NotNull(patchType);

        var harmony = new Harmony("e2e.siegeladder.authority");
        var repeatedHarmony = new Harmony("e2e.siegeladder.authority.repeated");
        try
        {
            var patched = harmony.CreateClassProcessor(patchType).Patch()
                ?? new List<MethodInfo>();
            var repeated = repeatedHarmony.CreateClassProcessor(patchType).Patch()
                ?? new List<MethodInfo>();

            Assert.Contains(patched, method => method.Name.Contains("set_State"));
            Assert.Contains(patched, method => method.Name.Contains("OnTick"));
            Assert.Contains(patched, method => method.Name.Contains("OnLadderStateChange"));
            Assert.Contains(repeated, method => method.Name.Contains("OnTick"));
        }
        finally
        {
            repeatedHarmony.UnpatchAll(repeatedHarmony.Id);
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Theory]
    [InlineData(
        SiegeLadder.LadderState.OnLand,
        SiegeLadder.LadderState.BeingRaised,
        "BeingRaisedStartFromGround")]
    [InlineData(
        SiegeLadder.LadderState.BeingPushedBack,
        SiegeLadder.LadderState.BeingRaised,
        "BeingPushedBackStopped")]
    [InlineData(
        SiegeLadder.LadderState.OnWall,
        SiegeLadder.LadderState.BeingRaised,
        "BeingPushedBackStartFromWall,BeingPushedBack,BeingPushedBackStopped")]
    [InlineData(
        SiegeLadder.LadderState.OnWall,
        SiegeLadder.LadderState.BeingPushedBack,
        "BeingPushedBackStartFromWall")]
    [InlineData(
        SiegeLadder.LadderState.BeingRaised,
        SiegeLadder.LadderState.BeingPushedBack,
        "BeingRaisedStopped")]
    [InlineData(
        SiegeLadder.LadderState.OnLand,
        SiegeLadder.LadderState.BeingPushedBack,
        "BeingRaisedStartFromGround,BeingRaised,BeingRaisedStopped")]
    [InlineData(
        SiegeLadder.LadderState.BeingRaised,
        SiegeLadder.LadderState.OnWall,
        "FallToWall")]
    [InlineData(
        SiegeLadder.LadderState.BeingPushedBack,
        SiegeLadder.LadderState.OnLand,
        "FallToLand")]
    public void GetLadderTransitionStates_ReconstructSkippedVanillaStates(
        SiegeLadder.LadderState currentState,
        SiegeLadder.LadderState targetState,
        string expected)
    {
        Assert.Equal(
            expected,
            string.Join(",", SiegeMachineStateReplicator.GetLadderTransitionStates(currentState, targetState)));
    }
}
