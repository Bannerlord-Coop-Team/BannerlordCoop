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
    public void EnteringCampaign_KeepsLoadingScreenUntilJoinTimeConverges()
    {
        // Arrange
        clientComponent.TestNetwork.CreatePeer();
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        var mapTimeTracker = clientComponent.Container.Resolve<Mock<IMapTimeTrackerInterface>>();
        _ = clientLogic.SetState<LoadingState>();
        loadingInterface.Reset();
        _ = clientLogic.SetState<CampaignState>();

        // Assert initial ready state
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkPlayerCampaignEntered>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Once);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());

        // Act: the replay marker only acknowledges that the held stream was applied.
        TestMessageBroker.Publish(this, new NetworkJoinReplayComplete());
        GameThread.Run(() => { }, blocking: true);

        // Assert replay acknowledgement
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinReplayApplied>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // Act: applying the first baseline immediately requests a fresh baseline behind the replay.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied());

        // Assert the map remains hidden while the refreshed baseline is in flight.
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCatchUpApplied>(
            clientComponent.TestNetwork.Peers[0]));
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
            clientComponent.TestNetwork.Peers[0]));
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(2));

        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // Act: a stale refreshed baseline requests a newer reliable baseline.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied());
        bool staleBaselineRefreshRequired = true;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out staleBaselineRefreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);

        Assert.Collection(
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]),
            _ => { },
            _ => { });
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(3));

        // Act: only a refreshed baseline that arrives current releases the joining player.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied());
        bool terminalRefreshRequired = false;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out terminalRefreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);

        // Assert catch-up release
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCatchUpApplied>(
            clientComponent.TestNetwork.Peers[0]));
    }

    [Fact]
    public void EnteringCampaignFromMission_DoesNotWaitForJoinCatchUpMarker()
    {
        // Arrange
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        var mapTimeTracker = clientComponent.Container.Resolve<Mock<IMapTimeTrackerInterface>>();
        _ = clientLogic.SetState<MissionState>();
        loadingInterface.Reset();

        // Act
        _ = clientLogic.SetState<CampaignState>();

        // Assert
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Never);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
    }
}
