#if DEBUG
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabHullInterpolationTests : NavalMissionTestEnvironment
{
    public NavalLabHullInterpolationTests(ITestOutputHelper output) : base(output) { }

    private void Start()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(First); Ready(Second); Tick(First); Tick(Second);
    }

    private JObject Status() => JObject.FromObject(Adapter(Second).Controller!.NativeControlStatus());
    private JToken Hull() => Status()["hullInterpolation"]!;
    private void Target(long sequence, float x, Guid probe = default)
    {
        var values = new float[24];
        for (int slot = 0; slot < 2; slot++)
        {
            values[slot * 12] = values[(slot * 12) + 4] = values[(slot * 12) + 8] = 1;
            values[(slot * 12) + 9] = x + slot;
        }
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, sequence, values, sequence * 10, probe));
    }

    private void Pose(float x)
    {
        Assert.Equal(x, Adapter(Second).Frames[0].origin.x, 4);
        Assert.Equal(x + 1, Adapter(Second).Frames[1].origin.x, 4);
        Assert.False(Adapter(Second).Authority);
    }

    [Fact]
    public void FirstExact_NextTargetInterpolatesBothHulls_ReplacementStartsAtLastWrittenPose()
    {
        Start(); Target(100, 0); Pose(0);
        Assert.Equal(1, Adapter(Second).ApplyCount);
        Target(101, 10); Pose(0);
        Assert.Equal(101, (long)Hull()["acceptedTargetSequence"]!);
        Assert.Equal(100, (long)Status()["lastAppliedFrameSequence"]!);
        Tick(Second, 0.01f); Pose(2);
        Assert.Equal(0.2f, (float)Hull()["applicationAlpha"]!, 4);
        Assert.Equal(1010, (long)Hull()["applicationSourceCallback"]!);
        Assert.False((bool)Hull()["applicationCompletedTarget"]!);
        Assert.Equal(2, (float)Hull()["lastNativeReadbackFrames"]![9]!, 4);
        Target(102, 20); Pose(2);
        Tick(Second, 0.025f); Pose(11);
        Target(101, -100); // A delayed source sequence cannot replace the accepted target.
        Assert.Equal(102, (long)Hull()["acceptedTargetSequence"]!);
        Tick(Second, 0.025f); Pose(20);
        Assert.Equal(102, (long)Status()["lastAppliedFrameSequence"]!);
        Assert.Equal(1020, (long)Status()["lastAppliedSourceCallback"]!);
        Assert.True((bool)Hull()["applicationCompletedTarget"]!);
        Assert.False((bool)Hull()["pending"]!);
        int writes = Adapter(Second).ApplyCount;
        Tick(Second, 10); Tick(Second, 10); Pose(20);
        Assert.Equal(writes, Adapter(Second).ApplyCount);
        Target(103, 30); Tick(Second, 0.025f); Pose(25);
        Assert.Equal(103, (long)Hull()["applicationTargetSequence"]!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidDeltaDoesNotAdvanceOrWrite(float dt)
    {
        Start(); Target(100, 0); Target(101, 10);
        int writes = Adapter(Second).ApplyCount;
        Tick(Second, dt); Pose(0);
        Assert.Equal(writes, Adapter(Second).ApplyCount);
        Tick(Second, 0.025f); Pose(5);
    }

    [Fact]
    public void LargeFiniteDeltaClampsAtExactEndpointWithoutExtrapolation()
    {
        Start(); Target(100, 0); Target(101, 10);
        Tick(Second, float.MaxValue); Pose(10);
        Assert.Equal(1, (float)Hull()["applicationAlpha"]!);
        Assert.Equal(2, Adapter(Second).ApplyCount);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("hold")]
    [InlineData("departure")]
    [InlineData("epoch")]
    [InlineData("authority")]
    [InlineData("dispose")]
    [InlineData("mission")]
    public void TerminalOrLifetimeLossCancelsPendingHullWrites(string kind)
    {
        Start(); Target(100, 0); Target(101, 10); Tick(Second, 0.01f);
        int writes = Adapter(Second).ApplyCount;
        var manifest = Manifest;
        if (kind == "stop") Execute("stop");
        if (kind == "departure")
        {
            First.Call(() => First.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-A", manifest.InstanceId)));
            PumpAll();
        }
        Second.Call(() =>
        {
            var controller = Adapter(Second).Controller!;
            if (kind == "hold") controller.Apply(new NetworkNavalLabAction(manifest.IncarnationId, Guid.NewGuid(), 1, "hold", 0, 0, false));
            if (kind == "epoch") Second.Resolve<IBattleHostRegistry>().Set(manifest.InstanceId, new BattleHostAssignment("naval-A", new[] { "naval-B" }, 2));
            if (kind == "authority") Assert.True(Second.Resolve<INetworkAgentRegistry>().TryTransferAuthority("changed", manifest.Combatants[1], 2));
            if (kind == "dispose") controller.Dispose();
            if (kind == "mission") controller.Mission = null;
        });
        Tick(Second, 0.05f); Tick(Second, 0.05f);
        Assert.Equal(writes, Adapter(Second).ApplyCount);
        Assert.False((bool)Hull()["pending"]!);
        Assert.Equal(JTokenType.Null, Hull()["targetFrames"]!.Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeApplyRefusalOrExceptionDuringInterpolationTerminallyHolds(bool throws)
    {
        Start(); Target(100, 0); Target(101, 10);
        Adapter(Second).FailApply = !throws; Adapter(Second).ThrowOnApply = throws;
        Tick(Second, 0.025f);
        Assert.True(Adapter(Second).TerminalHold);
        Assert.Equal(100, (long)Status()["lastAppliedFrameSequence"]!);
        Assert.Equal(1, (long)Hull()["applicationOrdinal"]!);
        int writes = Adapter(Second).ApplyCount;
        Tick(Second); Assert.Equal(writes, Adapter(Second).ApplyCount);
        Assert.False((bool)Hull()["pending"]!);
    }

    [Fact]
    public void SamplesWaitForEndpoint_AndSupersededTargetsNeverClaimAnAppliedEndpoint()
    {
        Start(); Target(100, 0);
        Guid probe = Guid.NewGuid();
        // The public native commands forbid probe controls; exercise the measurement seam directly.
        Second.Call(() => Assert.Equal("applied", Second.Resolve<INavalLabMeasurement>().Begin(probe, 1,
            (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency)));
        Target(101, 10, probe); Tick(Second, 0); Tick(Second, 0.01f);
        Assert.Empty((JArray)Samples(Second)["measurement"]!["samples"]!);
        Target(102, 20, probe);
        var superseded = Samples(Second)["measurement"]!["samples"]![0]!;
        Assert.Equal("superseded_before_interpolation_endpoint", (string?)superseded["error"]);
        Assert.Equal(JTokenType.Null, superseded["appliedCallback"]!.Type);
        Tick(Second, 0.05f); Tick(Second, 0);
        var complete = Samples(Second)["measurement"]!["samples"]![1]!;
        Assert.Equal(102, (long)complete["sequence"]!);
        Assert.Equal(JTokenType.Null, complete["error"]!.Type);
        Assert.True((long)complete["appliedCallback"]! < (long)complete["observedCallback"]!);
        Assert.Equal(1, (float)complete["native"]!["hullInterpolation"]!["applicationAlpha"]!);
    }
}
#endif
