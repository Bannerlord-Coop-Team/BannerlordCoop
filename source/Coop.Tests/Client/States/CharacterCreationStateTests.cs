using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;

        public CharacterCreationStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            serverPeer = MockNetwork.CreatePeer();
            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void NewPlayerHeroRegistered_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var playerHeroRegistered = new NewPlayerHeroRegistered(null, null);
            var payload = new MessagePayload<NetworkPlayerData>(
                this, new NetworkPlayerData(playerHeroRegistered));

            // Act
            characterCreationState.Handle_NetworkPlayerData(payload);

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void CharacterCreationFinished_Publishes_PackageMainHero()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var payload = new MessagePayload<CharacterCreationFinished>(
                this, new CharacterCreationFinished());

            // Act
            characterCreationState.Handle_CharacterCreationFinished(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<PackageMainHero>(message);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

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
