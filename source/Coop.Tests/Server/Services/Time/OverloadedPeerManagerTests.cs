using Autofac;
using Common.Messaging;
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
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);
        peer.SetQueueLength(AbovePauseThreshold);

        // Act
        manager.CheckForOverloadedPeers();

        // Assert
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
    }

    [Fact]
    public void OverloadedPeer_HoldsPauseUntilBelowResumeThreshold_ThenResumesAtOriginalSpeed()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        // Captured before pausing and restored on resume.
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_1x);

        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);

        // Overloaded -> pause.
        peer.SetQueueLength(AbovePauseThreshold);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());

        // Drained under the pause threshold but still above the resume threshold -> stay paused
        // (this is the hysteresis: no resume yet).
        peer.SetQueueLength(BetweenThresholds);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Never());

        // Drained below the resume threshold -> resume at the pre-pause speed.
        peer.SetQueueLength(BelowResumeThreshold);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
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
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Never());
    }

    [Fact]
    public void FinalJoinCatchUp_PausesOnlyAfterTwentySecondsAboveThreshold()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_1x);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartFinalCatchUp(connections, peer);
        peer.SetQueueLength(NetworkJoinSync.CompletionPacketThreshold + 1);
        DateTime startedUtc = DateTime.UtcNow;

        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay);

        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Never());

        manager.CheckForOverloadedPeers(
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromTicks(1));

        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
        Assert.Contains(
            serverComponent.TestMessageBroker.GetMessagesFromType<SendInformationMessage>(),
            message => message.Text == "Game paused; a joining client needs to catch up");
    }

    [Fact]
    public void FinalJoinCatchUp_ResumesAtAcceptedPacketThreshold()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_1x);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartFinalCatchUp(connections, peer);
        DateTime startedUtc = DateTime.UtcNow;

        peer.SetQueueLength(NetworkJoinSync.CompletionPacketThreshold + 1);
        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromTicks(1));

        peer.SetQueueLength(NetworkJoinSync.CompletionPacketThreshold);
        manager.CheckForOverloadedPeers(
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromSeconds(1));

        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
    }

    [Fact]
    public void FinalJoinCatchUp_DisconnectResumesPause()
    {
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_2x);
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = (OverloadedPeerManager)serverComponent.Container.Resolve<IOverloadedPeerManager>();
        var peer = AddConnectedPeer(connections);
        StartFinalCatchUp(connections, peer);
        peer.SetQueueLength(NetworkJoinSync.CompletionPacketThreshold + 1);
        DateTime startedUtc = DateTime.UtcNow;
        manager.CheckForOverloadedPeers(startedUtc);
        manager.CheckForOverloadedPeers(
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromTicks(1));

        serverComponent.TestMessageBroker.Publish(
            this,
            new PlayerDisconnected(peer, default));
        manager.CheckForOverloadedPeers(
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromSeconds(1));

        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_2x), Times.Once());
    }

    [Fact]
    public void LoadingPeerBeforeFinalCatchUp_DoesNotPauseAfterGracePeriod()
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
            startedUtc + OverloadedPeerManager.JoinCatchUpPauseDelay + TimeSpan.FromSeconds(1));

        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Never());
    }

    private LiteNetLib.NetPeer AddConnectedPeer(ConnectionCollection connections)
    {
        var peer = TestNetwork.CreatePeer();
        connections.PlayerJoiningHandler(new MessagePayload<PlayerConnected>(this, new PlayerConnected(peer)));
        return peer;
    }

    private void StartFinalCatchUp(ConnectionCollection connections, LiteNetLib.NetPeer peer)
    {
        serverComponent.Container.Resolve<IConnectionMessageQueue>().BeginQueueing(peer);
        var state = connections.ConnectionStates[peer].SetState<LoadingState>();

        state.PlayerCampaignEnteredHandler(
            new MessagePayload<NetworkPlayerCampaignEntered>(peer, new NetworkPlayerCampaignEntered()));
        DrainGameThread();
        SendAndDrain(state, peer, JoinSyncSignal.ReplayApplied);
        SendAndDrain(state, peer, JoinSyncSignal.BaselineRequested);
        SendAndDrain(state, peer, JoinSyncSignal.BaselineApplied);

        Assert.True(state.IsFinalCatchUpPending);
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
