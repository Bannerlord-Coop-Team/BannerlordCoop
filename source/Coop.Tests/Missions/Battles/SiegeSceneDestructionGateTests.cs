using GameInterface.Services.MapEvents;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class SiegeSceneDestructionGateTests : IDisposable
{
    [Fact]
    public void Seed_IsBoundToItsMission_AndConsumedOnce()
    {
        var siegeMission = NewMissionIdentity();
        var unrelatedMission = NewMissionIdentity();
        SiegeSceneDestructionGate.Begin(siegeMission, "map-event-17");

        Assert.False(SiegeSceneDestructionGate.TryTakeSeed(unrelatedMission, out _));
        Assert.True(SiegeSceneDestructionGate.TryTakeSeed(siegeMission, out var seed));
        Assert.NotEqual(0u, seed);
        Assert.False(SiegeSceneDestructionGate.TryTakeSeed(siegeMission, out _));
    }

    [Fact]
    public void SameMapEventId_ProducesTheSameSeedAcrossMissionInstances()
    {
        var firstMission = NewMissionIdentity();
        SiegeSceneDestructionGate.Begin(firstMission, "shared-map-event");
        Assert.True(SiegeSceneDestructionGate.TryTakeSeed(firstMission, out var first));

        var secondMission = NewMissionIdentity();
        SiegeSceneDestructionGate.Begin(secondMission, "shared-map-event");
        Assert.True(SiegeSceneDestructionGate.TryTakeSeed(secondMission, out var second));

        Assert.Equal(first, second);
    }

    [Fact]
    public void End_CancelsThePendingSeed()
    {
        var mission = NewMissionIdentity();
        SiegeSceneDestructionGate.Begin(mission, "map-event-18");

        SiegeSceneDestructionGate.End();

        Assert.False(SiegeSceneDestructionGate.TryTakeSeed(mission, out _));
    }

#pragma warning disable SYSLIB0050 // Identity-only test double; no native Mission constructor is invoked.
    private static Mission NewMissionIdentity() =>
        (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
#pragma warning restore SYSLIB0050

    public void Dispose() => SiegeSceneDestructionGate.End();
}
