#if DEBUG
using HarmonyLib;
using Missions.Naval;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 L6: a throw in the async agent and team tick is logged and finished only inside a co-op naval battle.</summary>
public class NavalTickFailurePatchesTests
{
    // In a co-op naval battle the tick completes and the throw is swallowed, so the game thread goes on.
    [Fact]
    public void Finalizer_InACoopNavalBattle_CompletesTheTickAndSwallowsTheThrow()
    {
        var mission = PendingMission();
        CoopNavalMissionScope.IsActive = true;
        try
        {
            Assert.Null(NavalAgentTickFailurePatch.Finalizer(mission, new NullReferenceException()));
            Assert.True(TickCompleted(mission));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = false;
        }
    }

    // Any other mission keeps vanilla's behaviour: the throw goes on and the tick stays pending.
    [Fact]
    public void Finalizer_OutsideACoopNavalBattle_RethrowsAndLeavesTheTick()
    {
        var mission = PendingMission();
        var thrown = new NullReferenceException();
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = false;
        try
        {
            Assert.Same(thrown, NavalAgentTickFailurePatch.Finalizer(mission, thrown));
            Assert.False(TickCompleted(mission));
            Assert.Null(NavalAgentTickFailurePatch.Finalizer(mission, null));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // The count starts again in each mission.
    [Fact]
    public void FailureCount_RestartsPerMission()
    {
        var count = new NavalTickFailureCount();
        object first = new object(), second = new object();

        Assert.Equal(1, count.Count(first));
        Assert.Equal(2, count.Count(first));
        Assert.Equal(1, count.Count(second));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(100, true)]
    [InlineData(200, false)]
    public void ShouldLog_TheFirstThreeThenEveryPowerOfTen(long count, bool expected)
    {
        Assert.Equal(expected, NavalTickFailureCount.ShouldLog(count));
    }

    private static Mission PendingMission()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        AccessTools.Field(typeof(Mission), "tickCompleted").SetValue(mission, false);
        return mission;
    }

    private static bool TickCompleted(Mission mission) => (bool)AccessTools.Field(typeof(Mission), "tickCompleted").GetValue(mission)!;
}
#endif
