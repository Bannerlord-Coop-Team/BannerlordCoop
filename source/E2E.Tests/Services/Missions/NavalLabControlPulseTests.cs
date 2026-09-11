#if DEBUG
using Common.Commands;
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabControlPulseTests : NavalMissionTestEnvironment
{
    public NavalLabControlPulseTests(ITestOutputHelper output) : base(output) { }
    private void Start(bool secondFirst = false, bool deploy = true)
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(secondFirst ? Second : First); Ready(secondFirst ? First : Second);
        Tick(First); Tick(Second);
        if (deploy) { Execute("complete-deployment"); Tick(First); Tick(Second); }
    }
    private CoopCommandResult Command(E2E.Tests.Environment.Instance.EnvironmentInstance instance, string name, params string[] values)
    {
        CoopCommandResult result = null!;
        instance.Call(() => result = instance.Resolve<ICoopCommandRegistry>().ProcessCommand("coop.debug.naval_lab." + name,
            new CoopCommandArgsFactory().FromValues(values)));
        PumpAll(); return result;
    }
    private CoopCommandResult Action(string kind, Guid id, int slot = 1) => Command(Server, "action", id.ToString(), kind, slot.ToString(), "0", "false");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredActionsRouteToOriginalOwnerNotElectedSimulator_ReceiptIsDispatchOnly(bool secondFirst)
    {
        Start(secondFirst);
        var operation = Guid.NewGuid();
        Assert.False(Command(Second, "action", operation.ToString(), "native-axes-pulse", "1", "0", "false").Succeeded);
        Assert.True(Action("native-axes-pulse", operation).Succeeded);
        Assert.Equal(operation, Assert.Single(Adapter(Second).AxesPulses).operationId);
        Assert.Equal(1, Adapter(Second).AxesPulses[0].ship);
        Assert.Empty(Adapter(First).AxesPulses);
        const string dispatch = "requested:simulated_native_boundary";
        Assert.Equal(dispatch, Receipt(Second, operation));
        Assert.True(Action("native-axes-pulse", operation).Succeeded);
        SendAction(Second, new NetworkNavalLabAction(Manifest.IncarnationId, operation, 1, "native-axes-pulse", 1, 0, false, DateTime.UtcNow.AddSeconds(1).Ticks));
        Assert.Single(Adapter(Second).AxesPulses);
        Assert.True(Command(Second, "control-status").Succeeded);
        Assert.Contains("simulated", Command(Second, "control-status").Output);
        Assert.False(Command(Second, "control-status", "extra").Succeeded);
        Assert.True(Action("native-axes-pulse", Guid.NewGuid()).Succeeded);
        Assert.Equal(1, Adapter(Second).AxesPulses.Last().ship);
        Assert.Equal(dispatch, Receipt(Second, operation));
        Assert.Empty(Adapter(First).NativeInputs); Assert.Empty(Adapter(Second).NativeInputs);
        Execute("stop");
        Assert.True(Action("native-axes-pulse", operation).Succeeded);
        Assert.Equal(2, Adapter(Second).AxesPulses.Count);
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("incarnation")]
    [InlineData("owner")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("not_ready")]
    [InlineData("terminal")]
    [InlineData("authority")]
    public void InvalidLifetimeOrAuthorityNeverInvokesNativeAdapter(string condition)
    {
        Start(deploy: condition != "not_ready");
        if (condition == "terminal") Execute("stop");
        if (condition == "authority")
        {
            Second.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[5], out var info);
            info.CurrentAuthority = Manifest.Controllers[0];
        }
        var action = new NetworkNavalLabAction(condition == "incarnation" ? Guid.NewGuid() : Manifest.IncarnationId,
            Guid.NewGuid(), condition == "epoch" ? 2 : 1, "native-axes-pulse", condition == "owner" ? 0 : 1, 0, false,
            DateTime.UtcNow.AddSeconds(condition == "expired" ? -1 : condition == "future" ? 10 : 1).Ticks);
        SendAction(Second, action);
        Assert.Empty(Adapter(Second).AxesPulses);
    }

    [Fact]
    public void EarlyServerCommandAndDisconnectedOwnerAreRejected()
    {
        Start(deploy: false);
        Assert.False(Action("native-axes-pulse", Guid.NewGuid()).Succeeded);
        Execute("complete-deployment"); Tick(First); Tick(Second);
        Second.Call(() => Second.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-B", Manifest.InstanceId)));
        PumpAll();
        Assert.False(Action("native-axes-pulse", Guid.NewGuid()).Succeeded);
        Assert.Empty(Adapter(Second).AxesPulses);
    }

    [Theory]
    [InlineData(NavalLabMode.Activation)]
    [InlineData(NavalLabMode.HeldHelm)]
    [InlineData(NavalLabMode.FactoryAuthorityProbe)]
    public void OldModesDoNotGainSyntheticAxesPulseActions(NavalLabMode mode)
    {
        CreateLab(mode); Ready(First); Ready(Second); Tick(First); Tick(Second);
        var operation = Guid.NewGuid();
        SendAction(First, new NetworkNavalLabAction(Manifest.IncarnationId, operation, 1, "native-axes-pulse", 0, 0, false, DateTime.UtcNow.AddSeconds(1).Ticks));
        Assert.Equal("rejected:wrong_mode", Receipt(First, operation));
        Assert.False(Action("native-axes-pulse", Guid.NewGuid()).Succeeded);
        Assert.Contains("wrong_mode", Command(First, "control-status").Output);
        Assert.Empty(Adapter(First).AxesPulses);
    }
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void CommandOwnerInputUsesExistingRelayIncludingHostLocalOwnerAndDeadman(bool secondFirst, int slot)
    {
        Start(secondFirst);
        var host = secondFirst ? Second : First; var follower = secondFirst ? First : Second;
        var owner = slot == 0 ? First : Second;
        Assert.True(Command(Server, "action", Guid.NewGuid().ToString(), "native-axes-pulse", slot.ToString(), "0.5", "true").Succeeded);
        var request = Assert.Single(Adapter(owner).AxesPulses);
        // The native view/converters are tested separately; this substitutes only their scalar output.
        var input = new NetworkNavalLabHelmInput(Manifest.IncarnationId, 1, slot, 1, request.deadline, true, 1, 1, 0, 0.7f, 2);
        owner.Call(() => Adapter(owner).SendInput!(input)); PumpAll();
        var received = Assert.Single(Adapter(host).NativeInputs);
        Assert.Equal(input.IncarnationId, received.IncarnationId); Assert.Equal(input.Ship, received.Ship);
        Assert.Equal(input.Sequence, received.Sequence); Assert.Equal(input.DeadlineUtcTicks, received.DeadlineUtcTicks);
        Assert.Equal(input.Rudder, received.Rudder); Assert.Equal(input.Sail, received.Sail);
        Assert.Empty(Adapter(follower).NativeInputs);
        var deadlines = typeof(NavalLabController).GetField("nativeInputDeadlines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((double[])deadlines.GetValue(Adapter(host).Controller)!)[slot] = 0.001;
        Tick(host); Assert.Contains(slot, Adapter(host).Neutralized); Assert.Empty(Adapter(follower).Neutralized);
        Assert.Empty(Adapter(host).HelmCalls); Assert.Empty(Adapter(follower).HelmCalls);
        Assert.Contains("hostAcceptedInputSequences", Command(host, "control-status").Output);
        Assert.Contains("no_local_native_view", Command(Server, "control-status").Output);
    }
}
#endif
