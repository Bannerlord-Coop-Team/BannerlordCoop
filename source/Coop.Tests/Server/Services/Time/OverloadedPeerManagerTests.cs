using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.Time;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Moq;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Time;

public class OverloadedPeerManagerTests
{
    private readonly ServerTestComponent serverComponent;

    private TestNetwork TestNetwork => serverComponent.TestNetwork;

    // Thresholds come from NetworkConfig: pause above MaxPacketsInQueue (10000), resume only once every
    // peer is back below ResumePacketsInQueue (5000).
    private const int AbovePauseThreshold = 15000;
    private const int BetweenThresholds = 7000;
    private const int BelowResumeThreshold = 4000;

    public OverloadedPeerManagerTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void OverloadedPeer_PausesTimeOnce()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);
        peer.SetQueueLength(AbovePauseThreshold);

        // Act
        manager.CheckForOverloadedPeers();

        // Assert
        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Once());
    }

    [Fact]
    public void OverloadedPeer_HoldsPauseUntilBelowResumeThreshold_ThenResumesAtOriginalSpeed()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        pauseLeaseMock.Setup(lease => lease.TryRelease())
            .Returns(true);

        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var unpausePolicy = timeControlMock.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ITimeControlInterface.AddUnpausePolicy))
            .Select(invocation => (Func<bool>)invocation.Arguments[0])
            .Single(policy => ReferenceEquals(policy.Target, manager));

        var peer = AddConnectedPeer(connections);

        // Overloaded -> pause.
        peer.SetQueueLength(AbovePauseThreshold);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Once());
        Assert.False(unpausePolicy());

        // Drained under the pause threshold but still above the resume threshold -> stay paused
        // (this is the hysteresis: no resume yet).
        peer.SetQueueLength(BetweenThresholds);
        manager.CheckForOverloadedPeers();
        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Never());

        // Drained below the resume threshold -> resume at the pre-pause speed.
        peer.SetQueueLength(BelowResumeThreshold);
        manager.CheckForOverloadedPeers();
        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Once());
        Assert.True(unpausePolicy());
    }

    [Fact]
    public void OverloadedPeer_WhenAnotherPolicyBlocksRestore_RetriesAfterPolicyAllowsIt()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        pauseLeaseMock.SetupSequence(lease => lease.TryRelease())
            .Returns(false)
            .Returns(true);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);

        peer.SetQueueLength(AbovePauseThreshold);
        manager.CheckForOverloadedPeers();
        peer.SetQueueLength(BelowResumeThreshold);

        manager.CheckForOverloadedPeers();
        manager.CheckForOverloadedPeers();

        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Exactly(2));
    }

    [Fact]
    public void LoadingPeer_DoesNotPauseTime()
    {
        // Arrange — a joining peer mid save-transfer: its queue is legitimately flooded by the
        // multi-MB transfer save and should not trigger normal live-peer backpressure.
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);
        connections.ConnectionStates[peer].SetState<Coop.Core.Server.Connections.States.TransferSaveState>();
        peer.SetQueueLength(AbovePauseThreshold);

        // Act
        manager.CheckForOverloadedPeers();

        // Assert — the transfer must not trigger a redundant "catching up" pause.
        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Never());
    }

    [Fact]
    public void InitialJoinCatchUp_DoesNotPauseAfterTwentySeconds()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartInitialCatchUp(connections, peer);
        peer.SetQueueLength(100);
        DateTime startedUtc = DateTime.UtcNow;

        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(
            startedUtc + TimeSpan.FromSeconds(20) - TimeSpan.FromTicks(1));

        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Never());

        manager.CheckForOverloadedPeers(startedUtc + TimeSpan.FromSeconds(20));

        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Never());
    }

    [Fact]
    public void FinalJoinCatchUp_DoesNotPauseWhileCatchingUp()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        pauseLeaseMock.Setup(lease => lease.TryRelease())
            .Returns(true);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartFinalCatchUp(connections, peer);
        DateTime startedUtc = DateTime.UtcNow;

        peer.SetQueueLength(100);
        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(startedUtc + TimeSpan.FromSeconds(20));

        peer.SetQueueLength(0);
        manager.CheckForOverloadedPeers(
            startedUtc + TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(1));

        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Never());

        var state = Assert.IsType<LoadingState>(connections.ConnectionStates[peer].State);
        SendAndDrain(state, peer, JoinSyncSignal.FinalBaselineApplied);
        SendAndDrain(state, peer, JoinSyncSignal.CatchUpApplied);
        manager.CheckForOverloadedPeers(
            startedUtc + TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(2));

        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Never());
    }

    [Fact]
    public void FinalJoinCatchUp_DisconnectDoesNotChangeTime()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        timeControlMock.Setup(t => t.ServerAcquireAutomaticPause())
            .Returns(pauseLeaseMock.Object);
        pauseLeaseMock.Setup(lease => lease.TryRelease())
            .Returns(true);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartFinalCatchUp(connections, peer);
        peer.SetQueueLength(NetworkJoinSync.CompletionPacketThreshold + 1);
        DateTime startedUtc = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(startedUtc + TimeSpan.FromSeconds(20));

        serverComponent.TestMessageBroker.Publish(
            this,
            new PlayerDisconnected(peer, default));
        manager.CheckForOverloadedPeers(
            startedUtc + TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(1));

        pauseLeaseMock.Verify(lease => lease.TryRelease(), Times.Never());
    }

    [Fact]
    public void LoadingPeerBeforeJoinCatchUp_DoesNotPauseAfterGracePeriod()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        connections.ConnectionStates[peer].SetState<LoadingState>();
        serverComponent.Container.Resolve<IConnectionMessageQueue>().BeginQueueing(peer);
        peer.SetQueueLength(AbovePauseThreshold);
        DateTime startedUtc = DateTime.UtcNow;

        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(
            startedUtc + TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(1));

        timeControlMock.Verify(
            t => t.ServerAcquireAutomaticPause(),
            Times.Never());
    }

    [Fact]
    public void StalledJoin_AbortsAndDiscardsReplayWithoutPausingLivePlayer()
    {
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var time = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var peer = AddConnectedPeer(connections);
        var state = StartInitialCatchUp(connections, peer);
        var started = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(started);
        manager.CheckForOverloadedPeers(started + NetworkJoinLimits.ReplayAppliedTimeout);

        Assert.True(state.IsAborted);
        Assert.Contains(peer, TestNetwork.DiscardedPeers);
        Assert.False(serverComponent.Container.Resolve<IConnectionMessageQueue>()
            .TryGetCatchUpPacketsRemaining(peer, out _));
        time.Verify(value => value.ServerAcquireAutomaticPause(), Times.Never());
        SendAndDrain(state, peer, JoinSyncSignal.BaselineRequested);
        Assert.True(state.IsAborted);
    }

    [Fact]
    public void JoinApplicationProgress_RenewsOnlyThatPeersWatchdog()
    {
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var first = AddConnectedPeer(connections);
        var second = AddConnectedPeer(connections);
        var progressing = StartInitialCatchUp(connections, first);
        var stalled = StartInitialCatchUp(connections, second);
        var started = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(started);
        SendAndDrain(progressing, first, JoinSyncSignal.BaselineRequested);
        manager.CheckForOverloadedPeers(started + NetworkJoinLimits.ReplayAppliedTimeout - TimeSpan.FromSeconds(1));
        manager.CheckForOverloadedPeers(started + NetworkJoinLimits.ReplayAppliedTimeout);
        Assert.False(progressing.IsAborted);
        Assert.True(stalled.IsAborted);
        serverComponent.Container.Resolve<Mock<ITimeControlInterface>>()
            .Verify(value => value.ServerAcquireAutomaticPause(), Times.Never());
    }

    [Fact]
    public void LoadingJoin_DoesNotPreventLiveOverloadPauseFromReleasing()
    {
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var time = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var lease = new Mock<IAutomaticPauseLease>();
        lease.Setup(value => value.TryRelease()).Returns(true);
        time.Setup(value => value.ServerAcquireAutomaticPause()).Returns(lease.Object);
        var joining = AddConnectedPeer(connections);
        var live = AddConnectedPeer(connections);
        StartInitialCatchUp(connections, joining);
        live.SetQueueLength(AbovePauseThreshold);
        var started = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(started);
        live.SetQueueLength(BelowResumeThreshold);
        manager.CheckForOverloadedPeers(started + TimeSpan.FromSeconds(25));
        time.Verify(value => value.ServerAcquireAutomaticPause(), Times.Once());
        lease.Verify(value => value.TryRelease(), Times.Once());
    }

    [Fact]
    public void PreEntryBacklog_ExpiresAtDeadlineWithoutPausingAndRejectsLateEntry()
    {
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var peer = AddConnectedPeer(connections);
        var state = connections.ConnectionStates[peer].SetState<LoadingState>();
        var queue = new Mock<IConnectionMessageQueue>();
        int heldPackets = AbovePauseThreshold;
        queue.Setup(value => value.TryGetCatchUpPacketsRemaining(peer, out heldPackets)).Returns(true);
        var terminator = new Mock<IJoinPeerTerminator>();
        using var manager = CreateWatchdog(queue.Object, terminator.Object);
        var started = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(started);
        manager.CheckForOverloadedPeers(started + NetworkJoinLimits.CampaignEntryTimeout - TimeSpan.FromTicks(1));
        Assert.False(state.IsAborted);
        terminator.Verify(value => value.Disconnect(peer, It.IsAny<string>()), Times.Never());
        manager.CheckForOverloadedPeers(started + NetworkJoinLimits.CampaignEntryTimeout);
        AssertAbortedBeforeEntry(state, peer);
        queue.Verify(value => value.AbortCatchUp(peer), Times.Once());
        terminator.Verify(value => value.Disconnect(peer, "JoinCampaignEntryTimeout"), Times.Once());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PreEntrySafetyLimit_AbortsImmediatelyAndRejectsLateEntry(bool overflowed, bool bytesExceeded)
    {
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var peer = AddConnectedPeer(connections);
        var state = connections.ConnectionStates[peer].SetState<LoadingState>();
        var queue = new Mock<IConnectionMessageQueue>();
        int heldPackets = 1;
        long pendingBytes = bytesExceeded ? NetworkJoinLimits.MaxReplayPendingBytes + 1 : 16;
        queue.Setup(value => value.TryGetCatchUpPacketsRemaining(peer, out heldPackets)).Returns(true);
        queue.Setup(value => value.TryGetCatchUpPendingBytes(peer, out pendingBytes)).Returns(true);
        queue.Setup(value => value.HasCatchUpOverflowed(peer)).Returns(overflowed);
        var terminator = new Mock<IJoinPeerTerminator>();
        using var manager = CreateWatchdog(queue.Object, terminator.Object);
        manager.CheckForOverloadedPeers(DateTime.UtcNow);
        AssertAbortedBeforeEntry(state, peer);
        queue.Verify(value => value.AbortCatchUp(peer), Times.Once());
        terminator.Verify(value => value.Disconnect(peer, "JoinReplayQueueLimit"), Times.Once());
    }

    private OverloadedPeerManager CreateWatchdog(IConnectionMessageQueue queue, IJoinPeerTerminator terminator) =>
        new(serverComponent.Container.Resolve<INetworkConfig>(), TestMessageBroker,
            new Lazy<INetwork>(() => TestNetwork),
            serverComponent.Container.Resolve<ITimeControlInterface>(),
            serverComponent.Container.Resolve<IConnectionCollection>(), queue, terminator);

    private IMessageBroker TestMessageBroker => serverComponent.TestMessageBroker;

    private void AssertAbortedBeforeEntry(LoadingState state, LiteNetLib.NetPeer peer)
    {
        Assert.True(state.IsAborted);
        state.PlayerCampaignEnteredHandler(
            new MessagePayload<NetworkPlayerCampaignEntered>(peer, new NetworkPlayerCampaignEntered()));
        DrainGameThread();
        Assert.True(state.IsAborted);
        Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
        serverComponent.Container.Resolve<Mock<ITimeControlInterface>>()
            .Verify(value => value.ServerAcquireAutomaticPause(), Times.Never());
    }

    private LiteNetLib.NetPeer AddConnectedPeer(ConnectionCollection connections)
    {
        var peer = TestNetwork.CreatePeer();
        peer.Setup(peer.Id, $"127.0.0.{connections.ConnectionStates.Count + 1}");
        connections.PlayerJoiningHandler(new MessagePayload<PlayerConnected>(this, new PlayerConnected(peer)));
        return peer;
    }

    private void StartFinalCatchUp(ConnectionCollection connections, LiteNetLib.NetPeer peer)
    {
        var state = StartInitialCatchUp(connections, peer);
        SendAndDrain(state, peer, JoinSyncSignal.BaselineRequested);
        SendAndDrain(state, peer, JoinSyncSignal.BaselineApplied);

        Assert.True(state.IsJoinCatchUpPending);
    }

    private LoadingState StartInitialCatchUp(ConnectionCollection connections, LiteNetLib.NetPeer peer)
    {
        serverComponent.Container.Resolve<IConnectionMessageQueue>().BeginQueueing(peer);
        var state = connections.ConnectionStates[peer].SetState<LoadingState>();

        state.PlayerCampaignEnteredHandler(
            new MessagePayload<NetworkPlayerCampaignEntered>(peer, new NetworkPlayerCampaignEntered()));
        DrainGameThread();
        SendAndDrain(state, peer, JoinSyncSignal.ReplayApplied);

        Assert.True(state.IsJoinCatchUpPending);
        return state;
    }

    private static void SendAndDrain(LoadingState state, LiteNetLib.NetPeer peer, JoinSyncSignal signal)
    {
        state.JoinSyncHandler(
            new MessagePayload<NetworkJoinSync>(peer, new NetworkJoinSync(signal)));
        DrainGameThread();
    }

    private static void DrainGameThread() =>
        Common.GameThread.Run(() => { }, blocking: true);
}
