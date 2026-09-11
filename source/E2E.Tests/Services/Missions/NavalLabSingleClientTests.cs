#if DEBUG
using Autofac;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabSingleClientTests : NavalMissionTestEnvironment
{
    public NavalLabSingleClientTests(ITestOutputHelper output) : base(output, 1) { }

    private Guid CreateSingle()
    {
        var operation = Guid.NewGuid();
        Server.Call(() => Server.Resolve<INavalLabCoordinator>().CreateSingle(operation, "naval-A"));
        PumpAll();
        return operation;
    }

    [Fact]
    public void OneConnectedOwner_ReadiesElectsAndCompletesDeploymentOnceWithoutFollower()
    {
        var create = CreateSingle();
        Assert.Single(Clients);
        Assert.Single(Manifest.Controllers);
        Assert.Single(Manifest.Ships);
        Assert.Equal(5, Manifest.Combatants.Length);
        Assert.Equal(NavalLabMode.SingleClientNative, Adapter(First).OpenedManifest!.Mode);
        Assert.False(Adapter(First).Authority);
        Assert.Throws<InvalidOperationException>(() => Execute("complete-deployment"));
        Ready(First);
        Assert.Single(Actions(First), action => action.Kind == "release");
        var hosts = Server.Resolve<IBattleHostRegistry>();
        Assert.True(hosts.TryGet(Manifest.InstanceId, out var host));
        Assert.Equal("naval-A", host.HostControllerId);
        Assert.Empty(host.SuccessorControllerIds);
        Tick(First);
        Assert.False(Adapter(First).Authority);
        var deploy = Execute("complete-deployment");
        Assert.Equal("deployed", Receipt(First, deploy));
        Execute("complete-deployment", operation: deploy);
        Assert.Equal(1, Adapter(First).DeploymentCalls);
        Assert.Equal("already_deployed", Receipt(First, Execute("complete-deployment")));
        Tick(First);
        Assert.True(Adapter(First).Authority);
        Assert.Empty(First.NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        Assert.Equal(0, Server.Resolve<NavalTestAdapterLoader>().LoadCount);
        Server.Call(() => Server.Resolve<INavalLabCoordinator>().CreateSingle(create, "naval-A"));
        PumpAll();
        Assert.Equal(1, Adapter(First).OpenCount);
    }

    [Theory]
    [InlineData("helm")]
    [InlineData("probe")]
    [InlineData("walk")]
    [InlineData("crew")]
    [InlineData("take-helm")]
    public void NativeMode_RejectsScriptedControlsAtServerAndRecipient(string kind)
    {
        CreateSingle();
        Ready(First);
        Assert.Throws<InvalidOperationException>(() => Execute(kind));
        var operation = Guid.NewGuid();
        SendAction(First, new NetworkNavalLabAction(Manifest.IncarnationId, operation, 1, kind, 0, 0, false));
        Assert.Equal("rejected:wrong_mode", Receipt(First, operation));
        Assert.Empty(Adapter(First).HelmCalls);
        Assert.Empty(Adapter(First).AgentControlCalls);
        Assert.Empty(Adapter(First).HeldHelmCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopBeforeOrAfterDeployment_IsTerminalAndDoesNotReopen(bool deploy)
    {
        CreateSingle();
        Ready(First);
        if (deploy) Execute("complete-deployment");
        var stop = Execute("stop");
        Execute("stop", operation: stop);
        Assert.True(Adapter(First).TerminalHold);
        Assert.False(Adapter(First).Authority);
        Assert.Equal("rejected:fixture_blocked", Adapter(First).CompleteDeployment());
        First.Call(() => Adapter(First).Controller!.AbortStart());
        PumpAll();
        Assert.Throws<InvalidOperationException>(() => CreateSingle());
    }

    [Fact]
    public void DeploymentFailure_HoldsAndCannotRetryOrEnableInput()
    {
        CreateSingle();
        Ready(First);
        Adapter(First).FailDeployment = true;
        Assert.Equal("failed:deployment", Receipt(First, Execute("complete-deployment")));
        Tick(First);
        Assert.True(Adapter(First).TerminalHold);
        Assert.False(Adapter(First).Authority);
        Assert.Throws<InvalidOperationException>(() => Execute("complete-deployment"));
        Assert.Equal(1, Adapter(First).DeploymentCalls);
    }

    [Fact]
    public void NativeOpenFailure_NeverReadiesOrDeploys()
    {
        Adapter(First).ThrowOnOpen = true;
        CreateSingle();
        Assert.Empty(Actions(First).Where(action => action.Kind == "release"));
        Assert.Equal(0, Adapter(First).DeploymentCalls);
        Assert.True(Adapter(First).Disposed);
        Assert.Throws<InvalidOperationException>(() => Execute("complete-deployment"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityOrActorMismatch_RejectsDeploymentAndTerminallyHolds(bool wrongAgent)
    {
        CreateSingle();
        Ready(First);
        First.Call(() =>
        {
            var registry = First.Resolve<global::Missions.INetworkAgentRegistry>();
            if (wrongAgent) Adapter(First).Agents[0] = Adapter(First).Agents[1];
            else Assert.True(registry.TryTransferAuthority("departed-owner", Manifest.Combatants[0]));
        });
        Assert.Equal("rejected:agent_authority_changed_or_unavailable", Receipt(First, Execute("complete-deployment")));
        Tick(First);
        Assert.True(Adapter(First).TerminalHold);
        Assert.False(Adapter(First).Authority);
        Assert.Equal(0, Adapter(First).DeploymentCalls);
    }

    [Fact]
    public void CampaignWriteBlocker_HoldsTheOnlyParticipant()
    {
        CreateSingle();
        Ready(First);
        Execute("complete-deployment");
        Server.Call(() => Server.Resolve<INavalLabSessionStore>().RejectCampaignWrite("single.regression"));
        Server.Call(() => Server.Resolve<Common.Messaging.IMessageBroker>().Publish(this,
            new GameInterface.Services.PlayerCaptivityService.Messages.CampaignTick()));
        PumpAll();
        Assert.True(Adapter(First).TerminalHold);
        Assert.Single(Actions(First), action => action.Kind == "hold");
    }
}
#endif
