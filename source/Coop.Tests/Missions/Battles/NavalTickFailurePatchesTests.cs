#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 L6: a throw in the async agent and team tick is logged and finished only inside a co-op naval battle.</summary>
[Collection("Mission.Current")]
public class NavalTickFailurePatchesTests
{
    // In a co-op naval battle the tick completes and the throw is swallowed, so the game thread goes on.
    [Fact]
    public void Finalizer_InACoopNavalBattle_CompletesTheTickAndSwallowsTheThrow()
    {
        var mission = PendingMission();
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = true;
        try
        {
            Assert.Null(NavalAgentTickFailurePatch.Finalizer(mission, new NullReferenceException()));
            Assert.True(TickCompleted(mission));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
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
    public void Report_CountsPerMission()
    {
        var log = new NavalTickFailureLog();
        object first = new object(), second = new object();

        Assert.Equal(1, log.Report(new InvalidOperationException(), "a test tick", first));
        Assert.Equal(2, log.Report(new InvalidOperationException(), "a test tick", first));
        Assert.Equal(1, log.Report(new InvalidOperationException(), "a test tick", second));
    }

    // The first three, every power of ten, and otherwise one line per summary interval with the running count.
    [Theory]
    [InlineData(1, 0.0, true)]
    [InlineData(3, 0.0, true)]
    [InlineData(4, 0.5, false)]
    [InlineData(10, 0.5, true)]
    [InlineData(11, 0.5, false)]
    [InlineData(100, 0.5, true)]
    [InlineData(200, 0.5, false)]
    [InlineData(257, 10.0, true)]
    [InlineData(258, 9.9, false)]
    public void ShouldLog_FirstThreePowersOfTenAndEveryTenSeconds(long count, double secondsSinceLastLine, bool expected)
    {
        Assert.Equal(expected, NavalTickFailureLog.ShouldLog(count, secondsSinceLastLine));
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
