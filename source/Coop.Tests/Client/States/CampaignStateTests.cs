using Autofac;
using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
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
    public void EnteringCampaign_KeepsLoadingScreenUntilCatchUpMarkerIsApplied()
    {
        // Arrange
        clientComponent.TestNetwork.CreatePeer();
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        _ = clientLogic.SetState<LoadingState>();
        loadingInterface.Reset();
        _ = clientLogic.SetState<CampaignState>();

        // Assert initial ready state
        Assert.Single(clientComponent.TestNetwork.GetPeerMessagesFromType<NetworkPlayerCampaignEntered>(
            clientComponent.TestNetwork.Peers[0]));
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Never);
        Assert.Empty(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());

        // Act
        TestMessageBroker.Publish(this, new NetworkJoinCatchUpComplete());
        GameThread.Run(() => { }, blocking: true);

        // Assert catch-up release
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
    }

    [Fact]
    public void EnteringCampaignFromMission_DoesNotWaitForJoinCatchUpMarker()
    {
        // Arrange
        var loadingInterface = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();
        _ = clientLogic.SetState<MissionState>();
        loadingInterface.Reset();

        // Act
        _ = clientLogic.SetState<CampaignState>();

        // Assert
        loadingInterface.Verify(m => m.HideLoadingScreen(), Times.Once);
        Assert.Single(TestMessageBroker.GetMessagesFromType<PlayerKillFeedColorResendRequested>());
    }
}
