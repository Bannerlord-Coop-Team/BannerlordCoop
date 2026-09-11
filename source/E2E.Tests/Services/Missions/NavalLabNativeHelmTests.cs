#if DEBUG
using Common.Commands;
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabNativeHelmTests : NavalMissionTestEnvironment
{
    public NavalLabNativeHelmTests(ITestOutputHelper output) : base(output) { }
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
        Assert.False(Command(Second, "action", operation.ToString(), "native-take-helm", "1", "0", "false").Succeeded);
        Assert.True(Action("native-take-helm", operation).Succeeded);
        Assert.Equal((operation, 1, true), Assert.Single(Adapter(Second).NativeHelmRequests));
        Assert.Empty(Adapter(First).NativeHelmRequests);
        const string dispatch = "dispatched:synthetic_native_helm_pending_observation";
        Assert.Equal(dispatch, Receipt(Second, operation));
        Assert.True(Action("native-take-helm", operation).Succeeded);
        SendAction(Second, new NetworkNavalLabAction(Manifest.IncarnationId, operation, 1, "native-take-helm", 1, 0, false, DateTime.UtcNow.AddSeconds(2).Ticks));
        Assert.Single(Adapter(Second).NativeHelmRequests);
        Assert.True(Command(Second, "helm-status").Succeeded);
        Assert.Contains("simulated", Command(Second, "helm-status").Output);
        Assert.False(Command(Second, "helm-status", "extra").Succeeded);
        Assert.True(Action("native-release-helm", Guid.NewGuid()).Succeeded);
        Assert.False(Adapter(Second).NativeHelmRequests.Last().take);
        Assert.Equal(dispatch, Receipt(Second, operation));
        Assert.Empty(Adapter(First).NativeInputs); Assert.Empty(Adapter(Second).NativeInputs);
        Execute("stop");
        Assert.True(Action("native-take-helm", operation).Succeeded);
        Assert.Equal(2, Adapter(Second).NativeHelmRequests.Count);
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
            Guid.NewGuid(), condition == "epoch" ? 2 : 1, "native-take-helm", condition == "owner" ? 0 : 1, 0, false,
            DateTime.UtcNow.AddSeconds(condition == "expired" ? -1 : condition == "future" ? 10 : 2).Ticks);
        SendAction(Second, action);
        Assert.Empty(Adapter(Second).NativeHelmRequests);
    }

    [Fact]
    public void EarlyServerCommandAndDisconnectedOwnerAreRejected()
    {
        Start(deploy: false);
        Assert.False(Action("native-take-helm", Guid.NewGuid()).Succeeded);
        Execute("complete-deployment"); Tick(First); Tick(Second);
        Second.Call(() => Second.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-B", Manifest.InstanceId)));
        PumpAll();
        Assert.False(Action("native-release-helm", Guid.NewGuid()).Succeeded);
        Assert.Empty(Adapter(Second).NativeHelmRequests);
    }

    [Theory]
    [InlineData(NavalLabMode.Activation)]
    [InlineData(NavalLabMode.HeldHelm)]
    [InlineData(NavalLabMode.FactoryAuthorityProbe)]
    public void OldModesDoNotGainSyntheticNativeHelmActions(NavalLabMode mode)
    {
        CreateLab(mode); Ready(First); Ready(Second); Tick(First); Tick(Second);
        var operation = Guid.NewGuid();
        SendAction(First, new NetworkNavalLabAction(Manifest.IncarnationId, operation, 1, "native-take-helm", 0, 0, false, DateTime.UtcNow.AddSeconds(2).Ticks));
        Assert.Equal("rejected:wrong_mode", Receipt(First, operation));
        Assert.False(Action("native-take-helm", Guid.NewGuid()).Succeeded);
        Assert.Contains("wrong_mode", Command(First, "helm-status").Output);
        Assert.Empty(Adapter(First).NativeHelmRequests);
    }
}
#endif
