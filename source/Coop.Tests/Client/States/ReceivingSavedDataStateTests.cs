using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Smithing;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ReceivingSavedDataStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;
        private readonly Mock<ILoadingInterface> loadingInterfaceMock;
        private readonly Mock<IGameStateInterface> gameStateMock;

        public ReceivingSavedDataStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
            gameStateMock = container.Resolve<Mock<IGameStateInterface>>();
        }

        private static NetworkGameSaveDataReceived SaveData(byte[] data, string campaignId) =>
            new NetworkGameSaveDataReceived(data, campaignId, new CraftingPlayerData(new(), new(), new()));

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
        public void NetworkGameSaveDataReceived_LoadsSaveGame_AndTransitionsToLoading()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();
            var gameSaveData = new byte[16];

            // Act
            currentState.Handle_NetworkGameSaveDataReceived(
                new MessagePayload<NetworkGameSaveDataReceived>(this, SaveData(gameSaveData, "12345")));

            // Assert — the world is reset to the main menu, then the received save is loaded.
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
            gameStateMock.Verify(x => x.LoadSaveGame(gameSaveData), Times.Once);
            Assert.IsType<LoadingState>(clientLogic.State);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Joining Coop Campaign",
                "Preparing host save data..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Loading host save data..."), Times.Once);
        }

        [Fact]
        public void NetworkGameSaveDataReceived_NullSaveData_StaysWaiting()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            currentState.Handle_NetworkGameSaveDataReceived(
                new MessagePayload<NetworkGameSaveDataReceived>(this, SaveData(null, "12345")));

            // Assert — no save to load, so we go to the main menu but remain waiting.
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
            gameStateMock.Verify(x => x.LoadSaveGame(It.IsAny<byte[]>()), Times.Never);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void NetworkGameSaveDataReceived_ZeroLenArray_StaysWaiting()
        {
            // Arrange
            var currentState = clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            currentState.Handle_NetworkGameSaveDataReceived(
                new MessagePayload<NetworkGameSaveDataReceived>(this, SaveData(Array.Empty<byte>(), "12345")));

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
            gameStateMock.Verify(x => x.LoadSaveGame(It.IsAny<byte[]>()), Times.Never);
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_GoesToMainMenu()
        {
            // Arrange
            clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void Disconnect_GoesToMainMenu()
        {
            // Arrange
            clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void Disconnect_Transitions_MainMenuState()
        {
            // Arrange
            clientLogic.SetState<ReceivingSavedDataState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            clientLogic.SetState<ReceivingSavedDataState>();

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
