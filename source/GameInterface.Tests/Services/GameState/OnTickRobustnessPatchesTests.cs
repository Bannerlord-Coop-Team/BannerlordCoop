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
}
