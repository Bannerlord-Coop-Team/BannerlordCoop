#if DEBUG
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabTwoClientNativeTests : NavalMissionTestEnvironment
{
    public NavalLabTwoClientNativeTests(ITestOutputHelper output) : base(output) { }

    private void Start(bool secondFirst = false)
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(secondFirst ? Second : First);
        Ready(secondFirst ? First : Second);
        Tick(First);
        Tick(Second);
    }

    private NetworkNavalLabHelmInput Input(int slot, long sequence = 1, bool helm = true, int epoch = 1, long? deadline = null) =>
        new(Manifest.IncarnationId, epoch, slot, sequence, deadline ?? DateTime.UtcNow.AddSeconds(1).Ticks,
            helm, 1, 1, 0, 0.35f, 2);

    [Fact]
    public void ControlStatus_FrameTimingAdvancesOnlyAfterSuccessfulFollowerApply()
    {
        Start(); Execute("complete-deployment"); Tick(First); Tick(Second);
        Newtonsoft.Json.Linq.JObject Status() => Newtonsoft.Json.Linq.JObject.FromObject(
            ((INavalNativeController)Adapter(Second).Controller!).NativeControlStatus());
        var values = new float[24];
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, 100, values, 700));
        var observed = Status();
        Assert.Equal(100, (long)observed["lastAppliedFrameSequence"]!);
        Assert.Equal(700, (long)observed["lastAppliedSourceCallback"]!);
        Assert.True((long)observed["lastAppliedUtcTicks"]! > 0);
        Assert.Equal(Newtonsoft.Json.Linq.JTokenType.Null, observed["hostSentFrameSequence"]!.Type);
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, 99, values, 701));
        Assert.Equal(observed["lastAppliedUtcTicks"], Status()["lastAppliedUtcTicks"]);
        Adapter(Second).FailApply = true;
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, 101, values, 702));
        var failed = Status();
        Assert.Equal(101, (long)failed["lastReceivedFrameSequence"]!);
        Assert.Equal(100, (long)failed["lastAppliedFrameSequence"]!);
        Assert.Equal(observed["lastAppliedSourceCallback"], failed["lastAppliedSourceCallback"]);
        Assert.Equal(observed["lastAppliedUtcTicks"], failed["lastAppliedUtcTicks"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothDeployAndFourOccupancyAcksGateInput_OriginalOwnerRoutesOnlyToElectedHost(bool secondFirst)
    {
        Start(secondFirst);
        var host = secondFirst ? Second : First;
        var follower = secondFirst ? First : Second;
        int remoteSlot = secondFirst ? 0 : 1;
        Assert.All(Clients, client => Assert.False(Adapter(client).InputAuthority!()));
        follower.Call(() => follower.Resolve<INetwork>().SendAll(Input(remoteSlot)));
        PumpAll();
        Assert.Empty(Adapter(host).NativeInputs);
        Guid deployment = Execute("complete-deployment");
        Assert.All(Clients, client =>
        {
            Assert.Equal("deployed", Receipt(client, deployment));
            Assert.Equal(1, Adapter(client).DeploymentCalls);
            Assert.Equal(2, Adapter(client).StationApplyCalls);
            Assert.False(Adapter(client).InputAuthority!());
        });
        Tick(host);
        Assert.False(Adapter(host).InputAuthority!());
        Tick(follower);
        Assert.All(Clients, client => Assert.True(Adapter(client).InputAuthority!()));
        foreach (var client in Clients)
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[i], out var info));
                Assert.Equal(Manifest.Controllers[i / 5], info.OriginalOwner);
                Assert.Equal(info.OriginalOwner, info.CurrentAuthority);
                Assert.Equal(1, info.AuthorityRevision);
            }
        }
        follower.Call(() => Adapter(follower).SendInput!(Input(remoteSlot, 2)));
        PumpAll();
        Assert.Equal(remoteSlot, Assert.Single(Adapter(host).NativeInputs).Ship);
        Assert.Empty(Adapter(follower).NativeInputs);
        follower.Call(() => Adapter(follower).SendInput!(Input(remoteSlot, 3, helm: false)));
        PumpAll();
        Assert.Contains(remoteSlot, Adapter(host).Neutralized);
        Execute("complete-deployment");
        Assert.All(Clients, client => Assert.Equal(1, Adapter(client).DeploymentCalls));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("epoch")]
    [InlineData("expired")]
    [InlineData("duplicate")]
    public void InvalidInputNeverReachesHostAdapter(string kind)
    {
        Start(); Execute("complete-deployment"); Tick(First); Tick(Second);
        var input = Input(1);
        Second.Call(() => Adapter(Second).SendInput!(input)); PumpAll();
        Assert.Single(Adapter(First).NativeInputs);
        var rejected = kind switch
        {
            "owner" => Input(0, 2), "epoch" => Input(1, 2, epoch: 2),
            "expired" => Input(1, 2, deadline: DateTime.UtcNow.AddSeconds(-1).Ticks), _ => input
        };
        Second.Call(() => Adapter(Second).SendInput!(rejected)); PumpAll();
        Assert.Single(Adapter(First).NativeInputs);
        Assert.Empty(Adapter(Second).NativeInputs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StationPartialFailureOrLostOccupancyHoldsBothClients(bool observation)
    {
        Start();
        Adapter(Second).ThrowOnStations = !observation;
        Adapter(Second).MissingOccupancy = observation;
        Execute("complete-deployment"); Tick(First); Tick(Second);
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        Assert.All(Clients, client => Assert.False(Adapter(client).InputAuthority!()));
        Assert.DoesNotContain(Actions(First), action => action.Kind == "native-controls-ready");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptionalStopCleanupRemainsTerminal(bool secondFirst)
    {
        Start(secondFirst); Execute("complete-deployment"); Tick(First); Tick(Second);
        Adapter(First).ThrowOnCancel = true;
        Execute("stop");
        Assert.True(Adapter(First).HoldCount > 0);
        Assert.False(Adapter(First).InputAuthority!());
        Assert.False(Adapter(Second).InputAuthority!());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepartureInvalidatesQueuedInputWithoutAgentAdoption(bool secondFirst)
    {
        Start(secondFirst); Execute("complete-deployment"); Tick(First); Tick(Second);
        var host = secondFirst ? Second : First;
        var follower = secondFirst ? First : Second;
        follower.Call(() => Adapter(follower).SendInput!(Input(secondFirst ? 0 : 1)));
        follower.Call(() => follower.Resolve<INetwork>().SendAll(new NetworkMissionLeft(
            secondFirst ? "naval-A" : "naval-B", Manifest.InstanceId)));
        Server.PumpGameThread(); PumpAll(); Tick(host);
        Assert.True(Adapter(host).TerminalHold);
        Assert.False(Adapter(host).InputAuthority!());
        Assert.Empty(Adapter(host).NativeInputs);
        for (int i = 0; i < 10; i++)
        {
            Assert.True(host.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[i], out var info));
            Assert.Equal(Manifest.Controllers[i / 5], info.CurrentAuthority);
        }
    }

    [Fact]
    public void EarlyStationAckFailsClosed()
    {
        Start();
        var stations = new NetworkNavalLabStations(Manifest.IncarnationId, 1, 0, "ack",
            Manifest.Combatants.Skip(1).Take(4).ToArray(), new[] { "a", "b", "c", "d" });
        First.Call(() => First.Resolve<INetwork>().SendAll(stations)); PumpAll();
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        Assert.DoesNotContain(Actions(First), action => action.Kind == "native-controls-ready");
    }

    [Fact]
    public void LocalDeadmanClearsHostInputWithoutFollowerSimulation()
    {
        Start(); Execute("complete-deployment"); Tick(First); Tick(Second);
        Second.Call(() => Adapter(Second).SendInput!(Input(1))); PumpAll();
        var deadlines = typeof(NavalLabController).GetField("nativeInputDeadlines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((double[])deadlines.GetValue(Adapter(First).Controller)!) [1] = 0.001;
        Tick(First);
        Assert.Contains(1, Adapter(First).Neutralized);
        Assert.Empty(Adapter(Second).Neutralized);
    }
}
#endif
