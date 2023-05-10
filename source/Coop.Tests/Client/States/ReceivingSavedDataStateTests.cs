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

namespace Coop.Tests.Client.States
{
    public class ReceivingSavedDataStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public ReceivingSavedDataStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, StubNetworkMessageBroker);
            clientLogic.State = new ReceivingSavedDataState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Setup
            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            // Execution
            clientLogic.State.Dispose();

            // Verification
            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void NetworkGameSaveDataRecieved_Publishes_EnterMainMenuEvent()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, new NetworkGameSaveDataReceived());

            // Verification
            Assert.True(isEventPublished);
        }

        [Fact]
        public void MainMenuEntered_Publishes_LoadGameSave()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            var gameObjectGuids = new GameObjectGuids(
                new string[] { "Random STR" });

            var networkMessage = new NetworkGameSaveDataReceived(
                new byte[] { 1 },
                "TestData",
                gameObjectGuids);

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, networkMessage);
            StubMessageBroker.Publish(this, new MainMenuEntered());

            // Verification
            Assert.True(isEventPublished);
        }

        [Fact]
        public void MainMenuEntered_Transitions_LoadingState()
        {
            // Setup
            var gameObjectGuids = new GameObjectGuids(new string[] { "Random STR" });

            var networkMessage = new NetworkGameSaveDataReceived(
                new byte[] { 1 },
                "TestData",
                gameObjectGuids);

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, networkMessage);
            StubMessageBroker.Publish(this, new MainMenuEntered());

            // Verification
            Assert.IsType<LoadingState>(clientLogic.State);
        }

        [Fact]
        public void MainMenuEntered_Handles_DefaultData()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, default(NetworkGameSaveDataReceived));
            StubMessageBroker.Publish(this, new MainMenuEntered());

            // Verification
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
            Assert.False(isEventPublished);
        }

        [Fact]
        public void MainMenuEntered_Handles_NullSaveData()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            var gameObjectGuids = new GameObjectGuids(new string[] { "Random STR" });

            var networkMessage = new NetworkGameSaveDataReceived(
                null,
                "TestData",
                gameObjectGuids);

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, networkMessage);
            StubMessageBroker.Publish(this, new MainMenuEntered());

            // Verification
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
            Assert.False(isEventPublished);
        }

        [Fact]
        public void MainMenuEntered_Handles_ZeroLenArray()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            var gameObjectGuids = new GameObjectGuids(new string[] { "Random STR" });

            var networkMessage = new NetworkGameSaveDataReceived(
                Array.Empty<byte>(),
                "TestData",
                gameObjectGuids);

            // Execution
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, networkMessage);
            StubMessageBroker.Publish(this, new MainMenuEntered());

            // Verification
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
            Assert.False(isEventPublished);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            // Execution
            clientLogic.EnterMainMenu();

            // Verification
            Assert.True(isEventPublished);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Setup
            var isEventPublished = false;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            // Execution
            clientLogic.Disconnect();

            // Verification
            Assert.True(isEventPublished);
        }

        [Fact]
        public void Disconnect_Transitions_EnterMainMenu()
        {
            // Execution
            clientLogic.Disconnect();

            // Verification
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
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
