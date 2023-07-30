using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ValidateModuleStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        public ValidateModuleStateTests(ITestOutputHelper output) : base(output)
        {
            serverPeer = MockNetwork.CreatePeer();
            clientLogic = ServiceProvider.GetService<IClientLogic>()!;
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            clientLogic.State = new ValidateModuleState(clientLogic);
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            clientLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void ValidateModuleState_EntryEvents()
        {
            // Act
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

            // Assert
            Assert.NotEmpty(MockNetwork.Peers);

            var message = Assert.Single(MockNetwork.GetPeerMessages(serverPeer));
            Assert.IsType<NetworkClientValidate>(message);
        }

        [Fact]
        public void NetworkClientValidated_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
            var validateState = new ValidateModuleState(clientLogic);
            clientLogic.State = validateState;

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
