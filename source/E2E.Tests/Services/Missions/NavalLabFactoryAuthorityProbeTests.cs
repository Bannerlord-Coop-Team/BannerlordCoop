#if DEBUG
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.MapEvents;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabFactoryAuthorityProbeTests : NavalMissionTestEnvironment
{
    public NavalLabFactoryAuthorityProbeTests(ITestOutputHelper output) : base(output) { }

    private void StartFactory(bool secondFirst = false)
    {
        CreateLab(NavalLabMode.FactoryAuthorityProbe);
        Ready(secondFirst ? Second : First);
        Ready(secondFirst ? First : Second);
        Tick(First);
        Tick(Second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ElectionPrecedesFactories_AndBothHydratedReceiptsPrecedeRelease(bool secondFirst)
    {
        CreateLab(NavalLabMode.FactoryAuthorityProbe);
        Assert.All(Clients, client => Assert.Empty(Adapter(client).Agents));
        var host = secondFirst ? Second : First;
        var follower = secondFirst ? First : Second;
        Ready(host);
        Tick(host);
        Assert.Equal(0, Adapter(host).MaterializeCount);
        Ready(follower);
        Assert.All(Clients, client => Assert.Equal(0, Adapter(client).MaterializeCount));
        Tick(host);
        Assert.True(Adapter(host).FactoryHost);
        Assert.Empty(Actions(host));
        Assert.Empty(host.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        CampaignRouter.PauseLink(follower.NetPeer, Server.NetPeer);
        Tick(follower);
        Assert.False(Adapter(follower).FactoryHost);
        Assert.Empty(Actions(host));
        Assert.Throws<InvalidOperationException>(() => Execute("helm"));
        CampaignRouter.ResumeLink(follower.NetPeer, Server.NetPeer);
        PumpAll();
        foreach (var client in Clients)
        {
            Assert.Single(Actions(client), action => action.Kind == "release");
            Assert.Equal(new[] { "scene_ready", "hydrated" }, client.NetworkSentMessages.GetMessages<NetworkNavalLabReceipt>()
                .Where(receipt => receipt.OperationId == Manifest.IncarnationId).Select(receipt => receipt.Status));
            Assert.Equal(10, Adapter(client).Agents.Length);
            Assert.Equal(1, Adapter(client).MaterializeCount);
            Ready(client);
            Assert.Equal(1, Adapter(client).MaterializeCount);
            for (int i = 0; i < 10; i++)
            {
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[i], out var info));
                Assert.Equal(Manifest.Controllers[i / 5], info.OriginalOwner);
                Assert.Equal(info.OriginalOwner, info.CurrentAuthority);
                Assert.Equal(1, info.AuthorityRevision);
            }
        }
        Tick(host);
        Tick(follower);
        Assert.True(Adapter(host).Authority);
        Assert.False(Adapter(follower).Authority);
        Assert.Equal(1, Adapter(follower).ApplyCount);
    }

    [Fact]
    public void EarlyReleaseAndFrameReceive_CannotPromoteUnhydratedFixture()
    {
        CreateLab(NavalLabMode.FactoryAuthorityProbe);
        Ready(First);
        Ready(Second);
        var release = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "release", 0, 0, false);
        SendAction(Second, release);
        Assert.Equal("rejected:not_hydrated", Receipt(Second, release.OperationId));
        First.Call(() => First.Resolve<MockBattleNetwork>().SendAll(new NetworkNavalLabFrames(
            Manifest.IncarnationId, 1, 9, new float[24], 1)));
        Assert.True(Second.PendingGameThreadActionCount > 0);
        // Materialization before draining the pre-hydration frame must not make that frame eligible.
        Second.Call(() => Adapter(Second).Controller!.OnMissionTick(0.05f));
        First.Call(() => Adapter(First).Controller!.OnMissionTick(0.05f));
        PumpAll();
        Assert.Equal(0, Adapter(Second).ApplyCount);
        Assert.Equal(1, (long)Samples(Second)["rejectedFrames"]!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryFailureOrIncompletePopulation_NeverHydratesAndHoldsPeer(bool incomplete)
    {
        Adapter(Second).ThrowOnMaterialize = !incomplete;
        Adapter(Second).IncompleteMaterialize = incomplete;
        StartFactory();
        Assert.DoesNotContain(Second.NetworkSentMessages.GetMessages<NetworkNavalLabReceipt>(), receipt => receipt.Status == "hydrated");
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        Assert.DoesNotContain(Actions(First), action => action.Kind == "release");
        Tick(Second);
        Assert.Equal(1, Adapter(Second).MaterializeCount);
        Assert.Equal("applied", Receipt(First, Execute("stop")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepartureDuringFactory_RejectsHydrationWithoutCrewAdoption(bool hostLeaves)
    {
        CreateLab(NavalLabMode.FactoryAuthorityProbe);
        Ready(First);
        Ready(Second);
        Adapter(Second).DuringMaterialize = () =>
        {
            var leaving = hostLeaves ? First : Second;
            leaving.Call(() => leaving.Resolve<INetwork>().SendAll(new NetworkMissionLeft(hostLeaves ? "naval-A" : "naval-B", Manifest.InstanceId)));
            Server.PumpGameThread();
        };
        Tick(Second);
        Assert.True(Adapter(Second).TerminalHold);
        Assert.DoesNotContain(Second.NetworkSentMessages.GetMessages<NetworkNavalLabReceipt>(), receipt => receipt.Status == "hydrated");
        Assert.Empty(Adapter(Second).Agents);
        Tick(First);
        Assert.True(Adapter(First).TerminalHold);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepartureWithQueuedFrame_TerminallyHoldsAndRejectsApply(bool hostLeaves)
    {
        StartFactory();
        First.Call(() => Adapter(First).Controller!.OnMissionTick(0.05f));
        Assert.True(Second.PendingGameThreadActionCount > 0);
        var leaving = hostLeaves ? First : Second;
        leaving.Call(() => leaving.Resolve<INetwork>().SendAll(new NetworkMissionLeft(hostLeaves ? "naval-A" : "naval-B", Manifest.InstanceId)));
        Server.PumpGameThread();
        PumpAll();
        Assert.Equal(0, Adapter(Second).ApplyCount);
        Tick(First);
        Tick(Second);
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        Assert.False(Adapter(First).Authority);
        foreach (var client in Clients)
            for (int i = 0; i < 10; i++)
            {
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[i], out var info));
                Assert.Equal(Manifest.Controllers[i / 5], info.CurrentAuthority);
            }
    }

    [Theory]
    [InlineData("walk")]
    [InlineData("turn")]
    [InlineData("jump")]
    [InlineData("crew")]
    public void RemoteOwnerPulse_RemainsOnOwnerWhileHelmGoesToElectedHost(string kind)
    {
        StartFactory();
        var helm = Execute("helm", 1, 0.25f, true);
        Assert.Equal("applied", Receipt(First, helm));
        Assert.Contains((1, 0.25f, true), Adapter(First).HelmCalls);
        Assert.Empty(Adapter(Second).HelmCalls);
        var pulse = Execute(kind, 1);
        Assert.Equal("applied", Receipt(Second, pulse));
        Assert.Empty(Adapter(First).AgentControlCalls);
        Assert.Single(Adapter(Second).AgentControlCalls);
        Execute(kind, 1, operation: pulse);
        Assert.Single(Adapter(Second).AgentControlCalls);
        var wrong = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, kind, 1, 0, false);
        SendAction(First, wrong);
        Assert.Equal("rejected:not_original_owner", Receipt(First, wrong.OperationId));
        Second.Call(() => Assert.True(Second.Resolve<INetworkAgentRegistry>().TryTransferAuthority("changed", Manifest.Combatants[5 + (kind == "crew" ? 1 : 0)])));
        Assert.Equal("rejected:agent_authority_changed_or_unavailable", Receipt(Second, Execute(kind, 1)));
    }

    [Fact]
    public void Frames_KeepSourceReceiveApplyObserveIdentity_AndRejectStaleTraffic()
    {
        StartFactory();
        Guid probe = Execute("probe", 1, 0.25f, true);
        First.Call(() => Adapter(First).Controller!.OnMissionTick(0.05f));
        var frame = Assert.Single(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        Assert.Equal(0, Adapter(Second).ApplyCount);
        PumpAll();
        Assert.Equal(1, Adapter(Second).ApplyCount);
        Assert.Empty((JArray)Samples(Second)["measurement"]!["samples"]!);
        Tick(Second, 0);
        var row = Assert.Single((JArray)Samples(Second)["measurement"]!["samples"]!);
        Assert.Equal(frame.Sequence, (long)row["sequence"]!);
        Assert.Equal(frame.SourceCallback, (long)row["hostSampleCallback"]!);
        Assert.Equal(JTokenType.Null, row["error"]!.Type);
        SendFrames(First, frame);
        SendFrames(First, new NetworkNavalLabFrames(Guid.NewGuid(), 1, 2, frame.Frames, 2, probe));
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 2, 2, frame.Frames, 2, probe));
        Assert.Equal(1, Adapter(Second).ApplyCount);
        Assert.Equal(3, (long)Samples(Second)["rejectedFrames"]!);
        Assert.NotSame(Adapter(First).Frames, Adapter(Second).Frames);
    }

    [Fact]
    public void FrameCallbackFault_HoldsInsteadOfPublishingSuccessfulObservation()
    {
        StartFactory();
        Execute("probe");
        Adapter(Second).ThrowOnApply = true;
        Tick(First);
        Assert.True(Adapter(Second).TerminalHold);
        Assert.True(Adapter(First).TerminalHold);
        Assert.Equal(0, (long)Samples(Second)["lastAppliedSequence"]!);
        Assert.Contains("simulated frame callback failure", (string?)Samples(Second)["measurement"]!["samples"]![0]!["error"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupAndRunDeadlines_TerminallyHoldWithoutRetry(bool released)
    {
        if (released) StartFactory();
        else CreateLab(NavalLabMode.FactoryAuthorityProbe);
        typeof(NavalLabController).GetField("factoryDeadline", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(Adapter(First).Controller, 0d);
        Tick(First);
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        var count = Adapter(First).MaterializeCount;
        Ready(First);
        Tick(First);
        Assert.Equal(count, Adapter(First).MaterializeCount);
        Assert.Single(First.NetworkSentMessages.GetMessages<NetworkNavalLabFault>());
    }

    [Theory]
    [InlineData("stop", false)]
    [InlineData("hold", false)]
    [InlineData("stop", true)]
    [InlineData("hold", true)]
    public void EmergencyCancellationFailure_AttemptsHoldBeforeDeduplicationAndBlocksFrames(string kind, bool holdThrows)
    {
        StartFactory();
        Execute("probe");
        Tick(First);
        // Keep a sampled follower frame pending when cancellation fails.
        Assert.Equal(1, Adapter(Second).ApplyCount);
        foreach (var client in Clients)
        {
            Adapter(client).ThrowOnCancel = true;
            Adapter(client).ThrowOnHold = holdThrows;
        }
        try
        {
            if (kind == "stop")
                Server.Call(() => Server.Resolve<INavalLabCoordinator>().Execute(Guid.NewGuid(), "stop", 0, 0, false));
            else
            {
                Server.Call(() => Server.Resolve<INavalLabSessionStore>().RejectCampaignWrite("terminal.regression"));
                Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
            }
            // Queue one more host frame behind the emergency action, without draining either client.
            First.Call(() => First.Resolve<MockBattleNetwork>().SendAll(new NetworkNavalLabFrames(
                Manifest.IncarnationId, 1, 99, new float[24], 99)));
            Assert.True(Second.PendingGameThreadActionCount >= 2);
            PumpAll();
            foreach (var client in Clients)
            {
                var action = Assert.Single(Actions(client), action => action.Kind == kind);
                Assert.Equal(1, Adapter(client).HoldCount);
                Assert.False((bool)typeof(NavalLabController).GetField("released", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(Adapter(client).Controller)!);
                Assert.Equal(!holdThrows, Adapter(client).TerminalHold);
                Assert.Equal(kind == "stop", Adapter(client).Mission.EndMissionCalled);
                Assert.StartsWith("failed:factory_probe.terminal_cleanup", Receipt(client, action.OperationId));
                Assert.Contains("simulated control cancellation failure", (string?)Samples(client)["factoryProbe"]!["factoryControlCleanupFailure"]);
                if (holdThrows)
                    Assert.Contains("simulated body hold failure", (string?)Samples(client)["factoryProbe"]!["factoryHoldFailure"]);
                SendAction(client, action);
                Assert.Equal(1, Adapter(client).HoldCount);
                SendAction(client, new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "release", 0, 0, false));
                var frames = client.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>().Count();
                Tick(client);
                Tick(client);
                Assert.Equal(frames, client.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>().Count());
            }
            Assert.Equal(1, Adapter(Second).ApplyCount);
            var pending = Assert.Single((JArray)Samples(Second)["measurement"]!["samples"]!);
            Assert.Equal("cancelled:" + kind, (string?)pending["error"]);
            Assert.Equal(JTokenType.Null, pending["observedCallback"]!.Type);
        }
        finally
        {
            foreach (var client in Clients) { Adapter(client).ThrowOnCancel = false; Adapter(client).ThrowOnHold = false; }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnLeavingCancellationFailure_AttemptsHoldAndCompletesNetworkTeardown(bool holdThrows)
    {
        StartFactory();
        Tick(First);
        Adapter(First).ThrowOnCancel = true;
        Adapter(First).ThrowOnHold = holdThrows;
        try
        {
            First.Call(() => typeof(NavalLabController).GetMethod("OnLeaving", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Adapter(First).Controller, null));
            Assert.Equal(1, Adapter(First).HoldCount);
            Assert.False((bool)typeof(NavalLabController).GetField("released", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Adapter(First).Controller)!);
            Assert.False(First.Resolve<MockBattleNetwork>().IsStarted);
            Assert.Single(First.NetworkSentMessages.GetMessages<NetworkMissionLeft>());
            foreach (var combatant in Manifest.Combatants)
                Assert.False(First.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(combatant, out _));
            Assert.Contains("simulated control cancellation failure", (string?)Samples(First)["factoryProbe"]!["factoryControlCleanupFailure"]);
            Tick(First);
        }
        finally { Adapter(First).ThrowOnCancel = false; Adapter(First).ThrowOnHold = false; }
    }

    [Fact]
    public void CampaignWriteHoldAndDuplicateStop_RetainIsolation()
    {
        StartFactory();
        Server.Call(() => Server.Resolve<INavalLabSessionStore>().RejectCampaignWrite("factory.regression"));
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
        PumpAll();
        Assert.All(Clients, client => Assert.True(Adapter(client).TerminalHold));
        var stop = Execute("stop");
        Execute("stop");
        Assert.All(Clients, client =>
        {
            Assert.Single(Actions(client), action => action.Kind == "stop");
            Assert.Equal("applied", Receipt(client, stop));
            Assert.True(Adapter(client).Mission.EndMissionCalled);
        });
    }
}
#endif
