#if DEBUG
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabHullInterpolationTests : NavalMissionTestEnvironment
{
    public NavalLabHullInterpolationTests(ITestOutputHelper output) : base(output) { }

    private void Start()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        foreach (var client in Clients) Adapter(client).CaptureShipSamples = false;
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        Execute("complete-deployment"); Tick(First); Tick(Second);
        Adapter(Second).Frames[1] = new MatrixFrame(Mat3.Identity, new Vec3(73, 0, 0));
    }

    private JToken Hull() => ShipStream(Second, 0);
    private void Target(long sequence, float x) => SendShipSample(First,
        Adapter(First).ShipSample(sequence, sequence * 10, new MatrixFrame(Mat3.Identity, new Vec3(x, 0, 0))));

    private void Pose(float x)
    {
        Assert.Equal(x, Adapter(Second).Frames[0].origin.x, 4);
        Assert.Equal(73, Adapter(Second).Frames[1].origin.x);
        Assert.All(Adapter(Second).ForeignFrameWrites, write => Assert.Equal(0, write.slot));
        Assert.Equal(0, (long)ShipStream(Second, 1)["appliedSequence"]!);
        Assert.DoesNotContain("apply", Adapter(Second).Calls);
    }

    [Fact]
    public void ForeignHullInterpolatesFromReadback_ReplacementStartsAtLastWrittenPose_OwnHullNeverChanges()
    {
        Start(); Target(100, 10); Pose(0);
        Assert.Empty(Adapter(Second).ForeignFrameWrites);
        Assert.Equal(100, (long)Hull()["acceptedSequence"]!);
        Assert.Equal(0, (long)Hull()["appliedSequence"]!);
        Tick(Second, 0.01f); Pose(2);
        Assert.Equal(0.2f, (float)Hull()["Alpha"]!, 4);
        Assert.Equal(1000, (long)Hull()["sourceCallback"]!);
        Assert.Equal(100, (long)Hull()["appliedSequence"]!);
        Assert.Equal(2, (float)Hull()["observedFrame"]![9]!, 4);
        Target(102, 20); Pose(2);
        Tick(Second, 0.025f); Pose(11);
        Target(101, -100);
        Assert.Equal(102, (long)Hull()["acceptedSequence"]!);
        Tick(Second, 0.025f); Pose(20);
        Assert.Equal(102, (long)Hull()["appliedSequence"]!);
        Assert.Equal(1020, (long)Hull()["sourceCallback"]!);
        Assert.Equal(1, (float)Hull()["Alpha"]!);
        int writes = Adapter(Second).ForeignFrameWrites.Count;
        Tick(Second, 10); Tick(Second, 10); Pose(20);
        Assert.Equal(writes, Adapter(Second).ForeignFrameWrites.Count);
        Target(103, 30); Tick(Second, 0.025f); Pose(25);
        Assert.Equal(103, (long)Hull()["appliedSequence"]!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidDeltaDoesNotAdvanceOrWrite(float dt)
    {
        Start(); Target(100, 10);
        Tick(Second, dt); Pose(0);
        Assert.Empty(Adapter(Second).ForeignFrameWrites);
        Assert.Equal(0, (long)Hull()["appliedSequence"]!);
        Tick(Second, 0.025f); Pose(5);
    }

    [Fact]
    public void LargeFiniteDeltaClampsAtExactEndpointWithoutExtrapolation()
    {
        Start(); Target(100, 10);
        Tick(Second, float.MaxValue); Pose(10);
        Assert.Equal(1, (float)Hull()["Alpha"]!);
        Assert.Single(Adapter(Second).ForeignFrameWrites);
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
        Start(); Target(100, 10); Tick(Second, 0.01f);
        int writes = Adapter(Second).ForeignFrameWrites.Count;
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
        Assert.Equal(writes, Adapter(Second).ForeignFrameWrites.Count);
        Assert.Equal(JTokenType.Null, Hull()["targetFrame"]!.Type);
        Pose(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeApplyRefusalOrExceptionDuringInterpolationTerminallyHolds(bool throws)
    {
        Start(); Target(100, 10); Tick(Second); Target(101, 20);
        var applied = (long)Hull()["AppliedCallback"]!;
        Adapter(Second).FailApply = !throws; Adapter(Second).ThrowOnApply = throws;
        Tick(Second, 0.025f);
        Assert.True(Adapter(Second).TerminalHold);
        Assert.Equal(100, (long)Hull()["appliedSequence"]!);
        Assert.Equal(applied, (long)Hull()["AppliedCallback"]!);
        Assert.Equal(1, (long)Hull()["ApplicationOrdinal"]!);
        int writes = Adapter(Second).ForeignFrameWrites.Count;
        Tick(Second); Assert.Equal(writes, Adapter(Second).ForeignFrameWrites.Count);
        Assert.Equal(JTokenType.Null, Hull()["targetFrame"]!.Type);
        Pose(10);
    }

    [Fact]
    public void SupersededUnwrittenTargetNeverClaimsAnApplication_AndCallbacksIdentifyPartialWrites()
    {
        Start(); Target(100, 10);
        Tick(Second, 0);
        Assert.Equal(0, (long)Hull()["appliedSequence"]!);
        Assert.Equal(0, (long)Hull()["ApplicationOrdinal"]!);
        Target(101, 20); Tick(Second, 0.01f); Pose(4);
        Assert.Equal(101, (long)Hull()["acceptedSequence"]!);
        Assert.Equal(101, (long)Hull()["appliedSequence"]!);
        Assert.Equal(1, (long)Hull()["ApplicationOrdinal"]!);
        Assert.True((long)Hull()["AcceptedCallback"]! < (long)Hull()["AppliedCallback"]!);
        Assert.Equal(0.2f, (float)Hull()["Alpha"]!, 4);
        Tick(Second, 0.05f); Pose(20);
        Assert.Equal(2, (long)Hull()["ApplicationOrdinal"]!);
        Assert.Equal(1, (float)Hull()["Alpha"]!);
    }
}
#endif
