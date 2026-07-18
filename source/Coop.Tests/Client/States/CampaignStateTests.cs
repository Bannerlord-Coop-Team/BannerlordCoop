using Autofac;
using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Interfaces;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using Moq;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States;

public class CampaignStateTests
{
    private readonly IClientLogic clientLogic;
    private readonly ClientTestComponent clientComponent;

    private TestMessageBroker TestMessageBroker => clientComponent.TestMessageBroker;
    public CampaignStateTests(ITestOutputHelper output)
    {
        clientComponent = new ClientTestComponent(output);
        var container = clientComponent.Container;
        clientLogic = container.Resolve<IClientLogic>()!;
    }

    [Fact(Skip = "Mission state not implemented and may be removed")]
    public void EnterMissionState_Publishes_EnterMissionState()
    {
        // Arrange
        var campaignState = clientLogic.SetState<CampaignState>();

        // Act
        clientLogic.EnterMissionState();

        // Assert
        //Assert.Single(TestMessageBroker.GetMessagesFromType<EnterMissionState>());
    }

    [Fact]
    public void MissionStateEntered_Transitions_MissionState()
    {
        // Arrange
        var campaignState = clientLogic.SetState<CampaignState>();

        var payload = new MessagePayload<MissionStateEntered>(
            this, new MissionStateEntered());

        // Act
        campaignState.Handle_MissionStateEntered(payload);

        // Assert
        Assert.IsType<MissionState>(clientLogic.State);
    }

    [Fact]
    public void EnterMainMenu_GoesToMainMenu()
    {
        // Arrange
        var campaignState = clientLogic.SetState<CampaignState>();
        var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

        // Act
        clientLogic.EnterMainMenu();

        // Assert
        gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
    }

    [Fact]
    public void Disconnect_GoesToMainMenu()
    {
        // Arrange
        var campaignState = clientLogic.SetState<CampaignState>();
        var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

        // Act
        clientLogic.Disconnect();

        // Assert
        gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
    }

    [Fact]
    public void OtherStateMethods_DoNotAlterState()
    {
        var campaignState = clientLogic.SetState<CampaignState>();

        clientLogic.Connect();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.Disconnect();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.ExitGame();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.LoadSavedData();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.StartCharacterCreation();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.EnterCampaignState();
        Assert.IsType<CampaignState>(clientLogic.State);

        clientLogic.ValidateModules();
        Assert.IsType<CampaignState>(clientLogic.State);
    }

    [Fact]
    public void EnteringCampaign_RetriesBaselinesAndWaitsForWorldReady()
    {
        clientComponent.TestNetwork.CreatePeer();
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        var mapTimeTracker = clientComponent.Container.Resolve<Mock<IMapTimeTrackerInterface>>();
        _ = clientLogic.SetState<LoadingState>();
        loadingInterface.Reset();
        _ = clientLogic.SetState<CampaignState>();

        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkPlayerCampaignEntered>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Once);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());

        PublishJoinSignal(JoinSyncSignal.ReplayComplete);
        Assert.Equal(1, JoinSignalCount(JoinSyncSignal.ReplayApplied));

        PublishJoinSignal(JoinSyncSignal.WorldReady);
        AssertJoinStillLoading(loadingInterface);

        PublishBaseline(success: false);
        Assert.Equal(1, JoinSignalCount(JoinSyncSignal.BaselineRequested));
        PublishBaseline(success: true);
        Assert.Equal(2, JoinSignalCount(JoinSyncSignal.BaselineRequested));
        PublishBaseline(success: false);
        Assert.Equal(3, JoinSignalCount(JoinSyncSignal.BaselineRequested));
        PublishBaseline(success: true);

        PublishTimeSample(mapTimeTracker, completed: false, refreshRequired: false);
        Assert.Equal(0, JoinSignalCount(JoinSyncSignal.BaselineApplied));
        PublishTimeSample(mapTimeTracker, completed: true, refreshRequired: true);
        Assert.Equal(4, JoinSignalCount(JoinSyncSignal.BaselineRequested));

        PublishBaseline(success: true);
        PublishTimeSample(mapTimeTracker, completed: true, refreshRequired: false);
        Assert.Equal(1, JoinSignalCount(JoinSyncSignal.BaselineApplied));

        PublishJoinSignal(JoinSyncSignal.WorldReady);
        AssertJoinStillLoading(loadingInterface);

        PublishBaseline(success: false);
        Assert.Equal(5, JoinSignalCount(JoinSyncSignal.BaselineRequested));
        PublishBaseline(success: true);
        PublishTimeSample(mapTimeTracker, completed: true, refreshRequired: true);
        Assert.Equal(6, JoinSignalCount(JoinSyncSignal.BaselineRequested));
        Assert.Equal(0, JoinSignalCount(JoinSyncSignal.FinalBaselineApplied));

        PublishBaseline(success: true);
        PublishTimeSample(mapTimeTracker, completed: true, refreshRequired: false);
        Assert.Equal(1, JoinSignalCount(JoinSyncSignal.FinalBaselineApplied));
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(8));
        AssertJoinStillLoading(loadingInterface);

        PublishJoinSignal(JoinSyncSignal.WorldReady);

        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
        Assert.Equal(1, JoinSignalCount(JoinSyncSignal.CatchUpApplied));
    }

    [Fact]
    public void EnteringCampaign_ShowsJoinPacketCountdownAndFinishingState()
    {
        clientComponent.TestNetwork.CreatePeer();
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        _ = clientLogic.SetState<LoadingState>();
        _ = clientLogic.SetState<CampaignState>();
        loadingInterface.Reset();

        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived(12345));
        GameThread.Run(() => { }, blocking: true);

        loadingInterface.Verify(m => m.SetLoadingMessage(
            "Loading Host Campaign",
            "Catching up to the host... 12,345 packets remaining"), Times.Once);

        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived(0));
        GameThread.Run(() => { }, blocking: true);

        loadingInterface.Verify(m => m.SetLoadingMessage(
            "Loading Host Campaign",
            "Finishing synchronization..."), Times.Once);
    }

    [Fact]
    public void EnteringCampaignFromMission_DoesNotWaitForJoinCatchUpMarker()
    {
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        var mapTimeTracker = clientComponent.Container.Resolve<Mock<IMapTimeTrackerInterface>>();
        _ = clientLogic.SetState<MissionState>();
        loadingInterface.Reset();

        _ = clientLogic.SetState<CampaignState>();

        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Never);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
    }

    private void PublishJoinSignal(JoinSyncSignal signal)
    {
        TestMessageBroker.Publish(this, new NetworkJoinSync(signal));
        DrainGameThread();
    }

    private void PublishBaseline(bool success) =>
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(success));

    private void PublishTimeSample(
        Mock<IMapTimeTrackerInterface> tracker,
        bool completed,
        bool refreshRequired)
    {
        tracker.Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired)).Returns(completed);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        DrainGameThread();
    }

    private int JoinSignalCount(JoinSyncSignal signal) =>
        clientComponent.TestNetwork
            .GetPeerMessagesFromType<NetworkJoinSync>(clientComponent.TestNetwork.Peers[0])
            .Count(message => message.Signal == signal);

    private void AssertJoinStillLoading(Mock<ILoadingInterface> loadingInterface)
    {
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
        Assert.Equal(0, JoinSignalCount(JoinSyncSignal.CatchUpApplied));
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
