#if DEBUG
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Heroes.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabLifecycleTests : NavalMissionTestEnvironment
{
    public NavalLabLifecycleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ActivationUnconfirmed_ReportsFaultAndStopsFramesInTheDiscoveringCallback()
    {
        StartReleased();
        Execute("probe", 1, 0.25f, true);
        Adapter(First).FailActivation = true;
        Adapter(First).HelmCalls.Clear();
        Tick(First);

        Assert.False(Adapter(First).Authority);
        Assert.Empty(Adapter(First).HelmCalls);
        Assert.Empty(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        Assert.Equal(0, Adapter(Second).ApplyCount);
        Assert.Single(First.NetworkSentMessages.GetMessages<NetworkNavalLabFault>());
        Assert.Single(Actions(Second), action => action.Kind == "hold");
        Assert.False(Adapter(Second).Authority);
        Tick(First);
        Assert.Empty(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        Guid stop = Execute("stop");
        Assert.Equal("applied", Receipt(First, stop));
        Assert.True(Adapter(First).Mission.EndMissionCalled);
        Assert.True(Adapter(Second).Mission.EndMissionCalled);
    }

    [Fact]
    public void Probe_RecordsReceiveApplyAndLaterObservation_WithoutRestartOnDuplicate()
    {
        StartReleased();
        Guid operation = Execute("probe", 1, 0.25f, true);
        Assert.Equal("applied", Receipt(First, operation));
        Assert.Equal("applied", Receipt(Second, operation));
        Assert.Empty(Adapter(Second).HelmCalls);
        int helmCount = Adapter(First).HelmCalls.Count;
        Execute("probe", 1, 0.25f, true, operation);
        foreach (var client in Clients)
            SendAction(client, Assert.Single(Actions(client), action => action.OperationId == operation));
        Assert.Equal(helmCount, Adapter(First).HelmCalls.Count);
        Assert.Equal(operation.ToString(), (string?)Samples(Second)["measurement"]!["operationId"]);

        First.Call(() => Adapter(First).Controller!.OnMissionTick(0.05f));
        Assert.True(Second.PendingGameThreadActionCount > 0);
        Assert.Equal(0, Adapter(Second).ApplyCount);
        Assert.Equal(0, (long)Samples(Second)["lastReceivedSequence"]!);
        Second.PumpGameThread();
        Assert.Equal(1, Adapter(Second).ApplyCount);
        Assert.Equal(1, (long)Samples(Second)["lastAppliedSequence"]!);
        Assert.Empty((JArray)Samples(Second)["measurement"]!["samples"]!);
        Tick(Second, 0);
        var sample = Assert.Single((JArray)Samples(Second)["measurement"]!["samples"]!);
        Assert.Equal(1, (long)sample["hostSampleCallback"]!);
        Assert.Equal(0, (long)sample["receivedCallback"]!);
        Assert.Equal(0, (long)sample["appliedCallback"]!);
        Assert.Equal(1, (long)sample["observedCallback"]!);
        Assert.Equal(JTokenType.Null, sample["error"]!.Type);
        Assert.True((bool)sample["native"]!["observed"]!["simulated"]!);
        Assert.Equal(10, ((JArray)sample["native"]!["authorities"]!).Count);
    }

    [Fact]
    public void FailedFrameApply_AdvancesReceiveOnly_AndCannotBecomeASuccessfulObservation()
    {
        StartReleased();
        Guid operation = Execute("probe");
        Adapter(Second).FailApply = true;
        Tick(First);
        var stats = Samples(Second);
        Assert.Equal(1, (long)stats["lastReceivedSequence"]!);
        Assert.Equal(0, (long)stats["lastAppliedSequence"]!);
        var failed = Assert.Single((JArray)stats["measurement"]!["samples"]!);
        Assert.Equal("unavailable:frames_not_applied", (string?)failed["error"]);
        Assert.Equal(JTokenType.Null, failed["observedCallback"]!.Type);
        Adapter(Second).FailApply = false;
        var firstFrame = Assert.Single(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        SendFrames(First, firstFrame);
        Assert.Equal(1, Adapter(Second).ApplyCount);
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, 2, firstFrame.Frames, 2, operation));
        Tick(Second, 0);
        Assert.Equal(2, (long)Samples(Second)["lastAppliedSequence"]!);
        Assert.Equal(2, ((JArray)Samples(Second)["measurement"]!["samples"]!).Count);
    }

    [Fact]
    public void ProbeReceiveHistory_IsBounded_AndSupersededFramesAreNotNativeObservations()
    {
        StartReleased();
        Guid operation = Execute("probe");
        Tick(First);
        var source = Assert.Single(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        // Exercise the receive budget without sleeping for host sample cadence or inventing a physics clock.
        for (int sequence = 2; sequence <= 70; sequence++)
            SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, sequence, source.Frames, sequence, operation));
        Tick(Second, 0);
        var measurement = Samples(Second)["measurement"]!;
        Assert.Equal(64, (int)measurement["capacity"]!);
        Assert.Equal(6, (int)measurement["overwritten"]!);
        Assert.Equal(8, ((JArray)measurement["samples"]!).Count);
        Assert.Equal(7, (long)measurement["samples"]![0]!["sequence"]!);
        Assert.Equal(69, (long)Samples(Second)["supersededSamples"]!);
        var lastPage = JObject.FromObject(Second.Resolve<INavalLabCoordinator>().Samples(69));
        var last = Assert.Single((JArray)lastPage["measurement"]!["samples"]!);
        Assert.Equal(70, (long)last["sequence"]!);
        Assert.Equal(JTokenType.Null, last["error"]!.Type);
    }

    [Fact]
    public void NativeStartupFailure_HoldsTheOtherClient_AndStopRemainsIdempotent()
    {
        Adapter(Second).ThrowOnOpen = true;
        CreateLab();
        Ready(First);
        Assert.True(Adapter(Second).Disposed);
        Assert.False(Second.Resolve<MockBattleNetwork>().IsStarted);
        Assert.Contains("simulated native open failure", Receipt(Second, Manifest.IncarnationId));
        Assert.Single(Actions(First), action => action.Kind == "hold");
        Assert.DoesNotContain(Actions(First), action => action.Kind == "release");
        Assert.Throws<InvalidOperationException>(() => Execute("helm"));
        Guid stop = Execute("stop");
        Execute("stop");
        Assert.Single(Actions(First), action => action.Kind == "stop");
        Assert.True(Adapter(First).Mission.EndMissionCalled);
        Assert.Equal("applied", Receipt(First, stop));
    }

    [Fact]
    public void Stop_CancelsProbeOnBothClients_AndDuplicateNeverReopensControls()
    {
        StartReleased();
        Execute("probe", 0, 0.5f, true);
        Tick(First);
        Guid stop = Execute("stop");
        Execute("stop");
        foreach (var client in Clients)
        {
            SendAction(client, Assert.Single(Actions(client), action => action.OperationId == stop));
            Assert.True(Adapter(client).Mission.EndMissionCalled);
            Assert.False(Adapter(client).Authority);
            Assert.True(Adapter(client).CancelCount > 0);
            Assert.Equal("cancelled:stop", (string?)Samples(client)["measurement"]!["status"]);
            Assert.Equal("applied", Receipt(client, stop));
        }
        Guid helm = Execute("helm", 0, 1);
        Assert.Equal("rejected:not_released_or_owner_departed", Receipt(First, helm));
    }

    [Fact]
    public void OwnerDeparture_PromotesEpochButHoldsPhysics_WithoutAdoptingCrew()
    {
        StartReleased();
        Execute("probe", 0, 0.25f, true);
        First.Call(() => First.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-A", Manifest.InstanceId)));
        PumpAll();
        AssertHost(Server, Manifest.InstanceId, "naval-B");
        Second.Call(() =>
        {
            Assert.True(Second.Resolve<IBattleHostRegistry>().TryGet(Manifest.InstanceId, out var host));
            Assert.Equal(2, host.Epoch);
        });
        Tick(Second);
        Assert.False(Adapter(Second).Authority);
        Assert.Empty(Adapter(Second).HelmCalls);
        Assert.Equal("cancelled:held_or_epoch_changed", (string?)Samples(Second)["measurement"]!["status"]);
        for (int i = 0; i < 5; i++)
        {
            Assert.True(Second.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(Manifest.Combatants[i], out var info));
            Assert.Equal("naval-A", info.CurrentAuthority);
            Assert.Equal("naval-A", info.OriginalOwner);
            Assert.Equal(1, info.AuthorityRevision);
        }
        Assert.Throws<InvalidOperationException>(() => Execute("crew", 0));
        var stale = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "helm", 0, 1, true);
        SendAction(Second, stale);
        Assert.Equal("rejected:stale_epoch", Receipt(Second, stale.OperationId));
        Assert.Empty(Adapter(Second).HelmCalls);
        Assert.Null(Server.Resolve<INavalLabSessionStore>().CampaignWriteBlocker);
    }

    [Fact]
    public void NonHostOwnerDeparture_HoldsEpochOneAuthorityAndFramesAcrossLaterTicks()
    {
        StartReleased();
        Execute("probe", 0, 0.25f, true);
        Tick(First);
        int sentBefore = First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>().Count();
        Adapter(First).Calls.Clear();
        Second.Call(() => Second.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-B", Manifest.InstanceId)));
        PumpAll();
        AssertHost(Server, Manifest.InstanceId, "naval-A");
        Assert.True(First.Resolve<IBattleHostRegistry>().TryGet(Manifest.InstanceId, out var assignment));
        Assert.Equal(1, assignment.Epoch);
        int cancellations = Adapter(First).CancelCount;
        Tick(First);
        Tick(First);
        Assert.False(Adapter(First).Authority);
        Assert.DoesNotContain("authority:True", Adapter(First).Calls);
        Assert.Equal(sentBefore, First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>().Count());
        Assert.True(Adapter(First).CancelCount > cancellations);
        Assert.Equal("cancelled:held_or_epoch_changed", (string?)Samples(First)["measurement"]!["status"]);
        Tick(Second, 0);
        var pending = Assert.Single((JArray)Samples(Second)["measurement"]!["samples"]!);
        Assert.Equal("cancelled:held_or_epoch_changed", (string?)pending["error"]);
        Assert.Equal(JTokenType.Null, pending["observedCallback"]!.Type);
        Assert.Throws<InvalidOperationException>(() => Execute("helm"));
    }

    [Fact]
    public void NonHostOwnerDeparture_RejectsFrameQueuedBeforeMembershipChange()
    {
        StartReleased();
        Tick(First);
        var source = Assert.Single(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        First.Call(() => First.Resolve<MockBattleNetwork>().SendAll(new NetworkNavalLabFrames(
            Manifest.IncarnationId, 1, 2, source.Frames, 2)));
        Assert.True(Second.PendingGameThreadActionCount > 0);
        Second.Call(() => Second.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-B", Manifest.InstanceId)));
        Server.PumpGameThread();
        // Assignment applies synchronously on receive, before the follower's queued frame work runs.
        AssertHost(Second, Manifest.InstanceId, "naval-A");
        PumpAll();
        Assert.Equal(1, Adapter(Second).ApplyCount);
        Assert.Equal(1, (long)Samples(Second)["lastAppliedSequence"]!);
        Assert.Equal(1, (long)Samples(Second)["rejectedFrames"]!);
    }

    [Theory]
    [InlineData("casualty")]
    [InlineData("result")]
    public void CampaignWrites_ReachRealServerGuards_AndHoldBothClients(string kind)
    {
        StartReleased();
        Execute("probe");
        First.Call(() =>
        {
            IMessage message = kind == "casualty"
                ? new NetworkRequestBattleCasualty("not-a-campaign-party", "not-a-campaign-troop", false, Manifest.InstanceId)
                : new NetworkBattleResultReady(Manifest.InstanceId, BattleState.AttackerVictory, 1);
            First.Resolve<INetwork>().SendAll(message);
        });
        Assert.Null(Server.Resolve<INavalLabSessionStore>().CampaignWriteBlocker);
        PumpAll();
        Assert.Equal(kind, Server.Resolve<INavalLabSessionStore>().CampaignWriteBlocker);
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
        PumpAll();
        foreach (var client in Clients)
        {
            Assert.Single(Actions(client), action => action.Kind == "hold");
            Assert.Equal("cancelled:hold", (string?)Samples(client)["measurement"]!["status"]);
            Assert.False(Adapter(client).Authority);
        }
        Assert.Empty(Server.InternalMessages.GetMessages<MapEventFinalized>());
        Assert.False(Server.Resolve<IBattleCompletionTracker>().TryConcludeAbandoned(
            Manifest.InstanceId, out _, out _, out _));
    }

    [Fact]
    public void DiskSave_IsDeniedByInstalledGamePatch_WithoutCallingSaveDriver()
    {
        StartReleased();
        var driver = new Mock<ISaveDriver>(MockBehavior.Strict);
        int callbacks = 0;
        Server.Call(() => Game.Current.Save(new MetaData(), "naval-e2e-must-not-write", driver.Object, result =>
        {
            Assert.Equal(SaveResult.GeneralFailure, result);
            callbacks++;
        }));
        Assert.Equal(1, callbacks);
        driver.VerifyNoOtherCalls();
        Assert.Empty(Server.InternalMessages.GetMessages<GameSaved>());
    }
}
#endif
