using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ValidateModuleStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;

        public ValidateModuleStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            serverPeer = MockNetwork.CreatePeer();
            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void ValidateModuleState_EntryEvents()
        {
            // Act
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Assert
            Assert.NotEmpty(MockNetwork.Peers);

            var message = Assert.Single(MockNetwork.GetPeerMessages(serverPeer));
            Assert.IsType<NetworkClientValidate>(message);
        }

        [Fact]
        public void NetworkClientValidated_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var heroExists = true;
            var payload = new MessagePayload<NetworkClientValidated>(
                this, new NetworkClientValidated(heroExists, "12345"));

            // Act
            validateState.Handle_NetworkClientValidated(payload);

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void NetworkClientValidated_Publishes_StartCharacterCreation()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var heroExists = false;
            var payload = new MessagePayload<NetworkClientValidated>(
                this, new NetworkClientValidated(heroExists, "12345"));

            // Act
            validateState.Handle_NetworkClientValidated(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<StartCharacterCreation>(message);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void MainMenuEntered_Transitions_MainMenuState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            validateState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void LoadSavedData_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void StartCharacterCreation_Publishes_StartCharacterCreation()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.StartCharacterCreation();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<StartCharacterCreation>(message);
        }

        [Fact]
        public void CharacterCreationStarted_Transitions_CharacterCreationState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<CharacterCreationStarted>(
                this, new CharacterCreationStarted());

            // Act
            validateState.Handle_CharacterCreationStarted(payload);

            // Assert
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }
    }
}
