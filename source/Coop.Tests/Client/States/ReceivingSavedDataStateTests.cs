using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using GameInterface.Services.Heroes.Data;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using GameInterface.Services.GameState.Messages;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Coop.Tests.Client.States
{
    public class ReceivingSavedDataStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public ReceivingSavedDataStateTests(ITestOutputHelper output) : base(output)
        {
            clientLogic = ServiceProvider.GetService<IClientLogic>()!;
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            clientLogic.State = new ReceivingSavedDataState(clientLogic);
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            clientLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void NetworkGameSaveDataReceived_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            var gameSaveData = new byte[16];
            var campaignId = "12345";

            var payload = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null));

            // Act
            currentState.Handle_NetworkGameSaveDataReceived(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void MainMenuEntered_Publishes_LoadGameSave()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            var gameSaveData = new byte[16];
            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Equal(2, MockMessageBroker.PublishedMessages.Count);
            var message = MockMessageBroker.PublishedMessages[1];
            var loadSaveMessage = Assert.IsType<LoadGameSave>(message);
            Assert.Equal(gameSaveData, loadSaveMessage.SaveData);

            Assert.IsType<LoadingState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_DefaultData()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(default, campaignId, null));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_NullSaveData()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(null, campaignId, null));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_ZeroLenArray()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            var gameSaveData = Array.Empty<byte>();
            var campaignId = "12345";

            var gameDataMessage = new MessagePayload<NetworkGameSaveDataReceived>(
                this, new NetworkGameSaveDataReceived(gameSaveData, campaignId, null));

            currentState.Handle_NetworkGameSaveDataReceived(gameDataMessage);

            var mainMenuPayload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            currentState.Handle_MainMenuEntered(mainMenuPayload);

            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
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
            var currentState = new ReceivingSavedDataState(clientLogic);
            clientLogic.State = currentState;

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
