using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        public CharacterCreationStateTests(ITestOutputHelper output) : base(output)
        {
            serverPeer = MockNetwork.CreatePeer();
            clientLogic = new ClientLogic(MockNetwork, MockMessageBroker);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            clientLogic.State = new CharacterCreationState(clientLogic);

            // Arrange
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            clientLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void HeroPackaged_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

            var heroBytes = new byte[10];
            var payload = new MessagePayload<NewHeroPackaged>(
                this, new NewHeroPackaged(heroBytes));

            // Act
            characterCreationState.Handle_NewHeroPackaged(payload);

            // Assert
            Assert.NotEmpty(MockNetwork.Peers);
            foreach(var peer in MockNetwork.Peers)
            {
                var message = Assert.Single(MockNetwork.GetPeerMessages(peer));
                Assert.IsType<NetworkTransferedHero>(message);
            }

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

        }

        [Fact]
        public void CharacterCreationFinished_Publishes_PackageMainHero()
        {
            // Arrange
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

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
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

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
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

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
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var characterCreationState = new CharacterCreationState(clientLogic);
            clientLogic.State = characterCreationState;

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
