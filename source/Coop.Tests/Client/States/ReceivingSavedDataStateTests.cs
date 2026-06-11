using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Smithing;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ReceivingSavedDataStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;
        private readonly Mock<ILoadingInterface> loadingInterfaceMock;

        public ReceivingSavedDataStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
        }

        [Fact]
        public void StateEntered_Shows_LoadingProgressMessage()
        {
            // Act
            clientLogic.SetState<ReceivingSavedDataState>();

            // Assert
            loadingInterfaceMock.Verify(x => x.ShowLoadingScreen(
                "Joining Coop Campaign",
                "Waiting for host save data..."), Times.Once);
        }

        [Fact]
        public void NetworkGameSaveDataReceived_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            var gameSaveData = new byte[16];
            var campaignId = "12345";

            var payload = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null, new CraftingPlayerData(new(), new(), new())));

            // Act
            currentState.Handle_NetworkGameSaveDataReceived(payload);

            // Assert
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<EnterMainMenu>(message);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Joining Coop Campaign",
                "Preparing host save data..."), Times.Once);
        }

        [Fact]
        public void MainMenuEntered_Publishes_LoadGameSave()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            var gameSaveData = new byte[16];
            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null, new CraftingPlayerData(new(), new(), new())));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Equal(2, clientComponent.TestMessageBroker.Messages.Count);
            var message = clientComponent.TestMessageBroker.Messages.ElementAt(1);
            var loadSaveMessage = Assert.IsType<LoadGameSave>(message);
            Assert.Equal(gameSaveData, loadSaveMessage.SaveData);

            Assert.IsType<LoadingState>(clientLogic.State);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Loading host save data..."), Times.Once);
        }

        [Fact]
        public void MainMenuEntered_Handles_DefaultData()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(default, campaignId, null, new CraftingPlayerData(new(), new(), new())));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_NullSaveData()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(null, campaignId, null, new CraftingPlayerData(new(), new(), new())));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_ZeroLenArray()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            var gameSaveData = Array.Empty<byte>();
            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null, new CraftingPlayerData(new(), new(), new())));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void Disconnect_Transitions_EnterMainMenu()
        {
            // Act
            clientLogic.Disconnect();

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }
    }
}
