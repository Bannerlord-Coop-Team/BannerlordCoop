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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StationMovement_BothOriginalOwnersWithholdOnlyCommittedCrew_AndTeardownClearsExperiment(bool secondFirst)
    {
        Start(secondFirst);
        var manifest = Manifest;
        foreach (var client in Clients)
        {
            Adapter(client).CommittedOarMovement = (incarnation, id, agent) =>
            {
                Assert.Equal(manifest.IncarnationId, incarnation);
                int index = Array.IndexOf(manifest.Combatants, id);
                Assert.NotEqual(0, index % 5);
                Assert.Same(Adapter(client).Agents[index], agent);
                return true; // Native occupancy is substituted, not claimed by this harness.
            };
        }
        Execute("complete-deployment"); Tick(First); Tick(Second);
        foreach (var client in Clients)
        {
            client.Call(() => client.Resolve<E2E.Tests.Environment.Mock.MockBattleNetwork>().NetworkSentPackets.Packets.Clear());
            Tick(client);
            client.Call(() =>
            {
                var rows = Newtonsoft.Json.Linq.JObject.FromObject(Adapter(client).Controller!.NativeControlStatus())["stationMovement"]!["rows"]!;
                Assert.Equal(4, rows.Count(row => (bool)row["LastEligible"]!));
                Assert.All(rows.Where(row => (bool)row["LastEligible"]!), row =>
                {
                    Assert.True((long)row["Withheld"]! > 0);
                    Assert.Equal(0, (long)row["EligibleSent"]!);
                });
                var packets = client.Resolve<E2E.Tests.Environment.Mock.MockBattleNetwork>().NetworkSentPackets
                    .GetPackets<global::Missions.Agents.Packets.MovementPacket>();
                Assert.All(packets.SelectMany(packet => packet.AgentIds), id => Assert.Equal(1, id % 5));
                Assert.Contains(rows, row => !(bool)row["LastEligible"]! && (long)row["Sent"]! > 0);
            });
        }
        Execute("stop");
        foreach (var client in Clients)
            client.Call(() =>
            {
                Adapter(client).Controller!.AbortStart();
                var status = Newtonsoft.Json.Linq.JObject.FromObject(Adapter(client).Controller!.NativeControlStatus())["stationMovement"]!;
                Assert.False((bool)status["enabled"]!);
                Assert.Empty(status["rows"]!);
            });
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("revision")]
    [InlineData("epoch")]
    [InlineData("mission")]
    [InlineData("hold")]
    public void StationMovement_ControllerRejectsStaleLifetimeBeforeNativeRead(string condition)
    {
        Start();
        var manifest = Manifest;
        int nativeReads = 0;
        Adapter(First).CommittedOarMovement = (_, _, _) => { nativeReads++; return true; };
        bool Eligible(int index)
        {
            var registry = First.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(manifest.Combatants[index], out var info));
            return (bool)HarmonyLib.AccessTools.Method(typeof(NavalLabController), "IsCommittedOarMovement")
                .Invoke(Adapter(First).Controller, new object[] { info })!;
        }
        First.Call(() => Assert.False(Eligible(1)));
        Assert.Equal(0, nativeReads);
        Execute("complete-deployment"); Tick(First); Tick(Second);
        First.Call(() =>
        {
            Assert.True(Eligible(1));
            nativeReads = 0;
            Assert.False(Eligible(0)); Assert.False(Eligible(6));
            Assert.Equal(0, nativeReads);
            if (condition == "authority" || condition == "revision")
                Assert.True(First.Resolve<INetworkAgentRegistry>().TryTransferAuthority(condition == "authority" ? "naval-B" : "naval-A", manifest.Combatants[1], 2));
            if (condition == "epoch") First.Resolve<IBattleHostRegistry>().Set(manifest.InstanceId, new BattleHostAssignment("naval-A", new[] { "naval-B" }, 2));
            if (condition == "mission") Adapter(First).Controller!.Mission = null;
            if (condition == "hold") Adapter(First).Controller!.Apply(new NetworkNavalLabAction(manifest.IncarnationId, Guid.NewGuid(), 1, "hold", 0, 0, false));
            Assert.False(Eligible(1));
            Assert.Equal(0, nativeReads);
        });
    }

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
