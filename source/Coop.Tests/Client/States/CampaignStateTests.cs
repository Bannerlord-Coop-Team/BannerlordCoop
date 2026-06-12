using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States;

public class CampaignStateTests
{
    private readonly IClientLogic clientLogic;
    private readonly ClientTestComponent clientComponent;

    private TestMessageBroker TestMessageBroker => clientComponent.TestMessageBroker;
    private TestNetwork TestNetwork => clientComponent.TestNetwork;

    private IClientState stateObservedOnCampaignEntered;
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
    public void ClientCampaignEntered_Subscribers_ObserveCampaignState()
    {
        // Arrange
        TestMessageBroker.Subscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);

        // Act
        var campaignState = clientLogic.SetState<CampaignState>();

        // Assert
        Assert.Same(campaignState, stateObservedOnCampaignEntered);
    }

    private void Handle_ClientCampaignEntered(MessagePayload<ClientCampaignEntered> payload)
    {
        stateObservedOnCampaignEntered = clientLogic.State;
    }

    [Fact]
    public void SetState_Publishes_ClientCampaignEntered()
    {
        // Act
        clientLogic.SetState<CampaignState>();

        // Assert
        Assert.Single(TestMessageBroker.GetMessagesFromType<ClientCampaignEntered>());
    }

    [Fact]
    public void SetState_Sends_NetworkPlayerCampaignEntered()
    {
        // Arrange
        var peer = TestNetwork.CreatePeer();

        // Act
        clientLogic.SetState<CampaignState>();

        // Assert
        Assert.Single(TestNetwork.GetPeerMessagesFromType<NetworkPlayerCampaignEntered>(peer));
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
}
