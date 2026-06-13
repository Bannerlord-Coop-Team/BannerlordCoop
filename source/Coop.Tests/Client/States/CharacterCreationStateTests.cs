using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface;
using GameInterface.Registry.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Interfaces;
using LiteNetLib;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        private readonly ClientTestComponent clientComponent;
        private readonly Mock<ILoadingInterface> loadingInterfaceMock;

        private TestMessageBroker TestMessageBroker => clientComponent.TestMessageBroker;
        private TestNetwork MockNetwork => clientComponent.TestNetwork;

        public CharacterCreationStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            serverPeer = MockNetwork.CreatePeer();
            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
        }

        [Fact]
        public void NewPlayerHeroRegistered_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var player = new Player("MyControllerId", "MyHeroId", "MyPartyId", "MyClanId", "MyCharacterObjectId");

            var payload = new MessagePayload<NetworkHeroRecieved>(
                this, new NetworkHeroRecieved(player));

            // Act
            characterCreationState.Handle_NetworkHeroRecieved(payload);

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void CharacterCreationFinished_Publishes_RegisterAllGameObjects()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var payload = new MessagePayload<CharacterCreationFinished>(
                this, new CharacterCreationFinished());

            // Act
            characterCreationState.Handle_CharacterCreationFinished(payload);

            // Assert
            Assert.Single(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));
            loadingInterfaceMock.Verify(x => x.ShowLoadingScreen(
                "Joining Coop Campaign",
                "Sending your character to the host..."), Times.Once);
        }

        [Fact]
        public void EnterMainMenu_GoesToMainMenu()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void MainMenuEntered_Transitions_MainMenuState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            characterCreationState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void LoadSavedData_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }
    }
}
