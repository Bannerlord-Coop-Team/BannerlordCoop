using GameInterface.Services.GameState.Patches;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class OnTickRobustnessPatchesTests
{
    [Fact]
    public void MissionOnTickFinalizer_WithException_CompletesTick()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        mission.tickCompleted = false;

        OnTickRobustnessPatches.Finalizer_MissionOnTick(mission, new InvalidOperationException());

        Assert.True(mission.tickCompleted);
    }

    [Fact]
    public void MissionOnTickFinalizer_WithoutException_LeavesTickPending()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        mission.tickCompleted = false;

        OnTickRobustnessPatches.Finalizer_MissionOnTick(mission, null);

        Assert.False(mission.tickCompleted);
    }

    [Fact]
    public void AgentTickFinalizer_WithException_CompletesTheTickAndSwallowsIt()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        mission.tickCompleted = false;

        var rethrown = OnTickRobustnessPatches.Finalizer_TickAgentsAndTeams(mission, new NullReferenceException());

        Assert.Null(rethrown);
        Assert.True(mission.tickCompleted);
    }

    [Fact]
    public void AgentTickFinalizer_WithoutException_LeavesTheTickToTheAgentTick()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        mission.tickCompleted = false;

        var rethrown = OnTickRobustnessPatches.Finalizer_TickAgentsAndTeams(mission, null);

        Assert.Null(rethrown);
        Assert.False(mission.tickCompleted);
    }

    [Fact]
    public void FixedTickFinalizer_SwallowsTheException()
    {
        Assert.Null(OnTickRobustnessPatches.Finalizer_MissionOnFixedTick(new InvalidOperationException()));
        Assert.Null(OnTickRobustnessPatches.Finalizer_MissionOnFixedTick(null));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(100, true)]
    [InlineData(200, false)]
    [InlineData(1000, true)]
    public void ShouldLogFailure_LogsTheFirstThreeThenEveryPowerOfTen(long count, bool expected)
    {
        Assert.Equal(expected, OnTickRobustnessPatches.ShouldLogFailure(count));
    }
}
