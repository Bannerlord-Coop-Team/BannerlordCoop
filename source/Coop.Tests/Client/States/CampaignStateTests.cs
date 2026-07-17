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

        // A world-ready marker cannot release the map before both complete baselines are applied.
        TestMessageBroker.Publish(this, new NetworkJoinWorldReady());
        GameThread.Run(() => { }, blocking: true);
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // A failed baseline requests another complete snapshot.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(false));

        // The map remains hidden while baseline retries are in flight.
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCatchUpApplied>(
            clientComponent.TestNetwork.Peers[0]));
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
            clientComponent.TestNetwork.Peers[0]));
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(2));

        // The first successful baseline still requests a second successful snapshot.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(true));

        Assert.Collection(
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]),
            _ => { },
            _ => { });
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(3));

        // Failed retries do not count toward the two required successful baselines.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(false));
        Assert.Equal(
            3,
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]).Count());
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(4));

        // The second successful baseline starts join-only time convergence.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(true));

        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCatchUpApplied>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        bool refreshRequired = false;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired))
            .Returns(false);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));

        // A time sample showing a stale baseline requests a newer full party snapshot.
        refreshRequired = true;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        Assert.Equal(
            4,
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]).Count());
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(5));

        // The refreshed initial baseline must converge before the client asks for the final cut.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(true));
        refreshRequired = false;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinFinalCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(6));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // A world-ready marker cannot release the map before the distinct final baseline completes.
        TestMessageBroker.Publish(this, new NetworkJoinWorldReady());
        GameThread.Run(() => { }, blocking: true);
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // A failed final snapshot stays in the final phase and requests another complete baseline.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(false));
        Assert.Equal(
            5,
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]).Count());
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(7));

        // A stale final snapshot also retries without acknowledging or revealing the map.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(true));
        refreshRequired = true;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        Assert.Equal(
            6,
            clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCampaignBaselineRequested>(
                clientComponent.TestNetwork.Peers[0]).Count());
        Assert.Empty(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinFinalCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));
        mapTimeTracker.Verify(m => m.ResetForCampaignJoin(), Times.Exactly(8));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // Only a current final snapshot can acknowledge the final barrier.
        TestMessageBroker.Publish(this, new JoinCampaignBaselineApplied(true));
        refreshRequired = false;
        mapTimeTracker
            .Setup(m => m.TryCompleteCampaignJoinCatchUp(out refreshRequired))
            .Returns(true);
        TestMessageBroker.Publish(this, new CampaignTimeSampleReceived());
        GameThread.Run(() => { }, blocking: true);
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinFinalCampaignBaselineApplied>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);

        // The ordered world-stream tail releases the joining player after the final acknowledgement.
        TestMessageBroker.Publish(this, new NetworkJoinWorldReady());
        GameThread.Run(() => { }, blocking: true);

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
