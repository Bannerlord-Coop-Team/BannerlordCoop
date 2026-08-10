using Autofac;
using Common;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Common.Session;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.JoinCancel;
using GameInterface.Services.UI.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;
        private readonly Mock<ILoadingInterface> loadingInterfaceMock;
        private readonly Mock<IJoinAttemptOverlay> overlayMock;
        private readonly JoinAttemptPresentation joinAttempt;

        public MainMenuStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
            overlayMock = container.Resolve<Mock<IJoinAttemptOverlay>>();
            joinAttempt = container.Resolve<JoinAttemptPresentation>();
        }

        private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

        private MainMenuState StartDialing()
        {
            var state = clientLogic.SetState<MainMenuState>();
            clientLogic.Connect();
            DrainGameThread();
            loadingInterfaceMock.Invocations.Clear();
            overlayMock.Invocations.Clear();
            clientComponent.TestMessageBroker.Messages.Clear();
            return state;
        }

        private static MessagePayload<T> Payload<T>(T message) where T : IMessage =>
            new MessagePayload<T>(null, message);

        [Fact]
        public void Connecting_RaisesTheConnectingScreenWithACancel()
        {
            clientLogic.SetState<MainMenuState>();
            clientLogic.Connect();
            DrainGameThread();

            loadingInterfaceMock.Verify(x => x.ShowLoadingScreen(
                joinAttempt.Title, joinAttempt.Description), Times.Once);
            overlayMock.Verify(x => x.Show(joinAttempt.CancelLabel), Times.Once);
        }

        [Fact]
        public void ReEnteringOnSessionEnd_ShowsNoConnectingScreen()
        {
            // MissionState, CharacterCreationState and ReceivingSavedDataState all re-enter this
            // state when a session ends, with no join in flight to show a screen for.
            clientLogic.SetState<MainMenuState>();
            DrainGameThread();

            loadingInterfaceMock.Verify(x => x.ShowLoadingScreen(
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            overlayMock.Verify(x => x.Show(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ReEnteringOnSessionEnd_IgnoresCancel()
        {
            var state = clientLogic.SetState<MainMenuState>();
            DrainGameThread();
            clientComponent.TestMessageBroker.Messages.Clear();

            state.Handle_CancelJoinAttempt(Payload(new CancelJoinAttempt()));
            DrainGameThread();

            Assert.Empty(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void NetworkConnected_TakesCancelDownAndLeavesTheWindowToTheNextState()
        {
            var state = StartDialing();

            state.Handle_NetworkConnected(Payload(new NetworkConnected()));
            DrainGameThread();

            overlayMock.Verify(x => x.Hide(), Times.Once);
            loadingInterfaceMock.Verify(x => x.HideLoadingScreen(), Times.Never);
        }

        [Fact]
        public void CancelJoinAttempt_TakesTheScreenDownAndEndsTheSession()
        {
            var state = StartDialing();

            state.Handle_CancelJoinAttempt(Payload(new CancelJoinAttempt()));
            DrainGameThread();

            overlayMock.Verify(x => x.Hide(), Times.AtLeastOnce);
            loadingInterfaceMock.Verify(x => x.HideLoadingScreen(), Times.AtLeastOnce);
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void CancelJoinAttempt_AfterTheHandshakeLands_LeavesTheSessionAlone()
        {
            var state = StartDialing();
            state.Handle_NetworkConnected(Payload(new NetworkConnected()));
            DrainGameThread();
            clientComponent.TestMessageBroker.Messages.Clear();

            state.Handle_CancelJoinAttempt(Payload(new CancelJoinAttempt()));
            DrainGameThread();

            Assert.Empty(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void Dispose_WhileDialing_TakesTheWholeScreenDown()
        {
            // Container teardown after a rejected or failed start disposes this state with no
            // transition; missing it strands the player behind the loading screen.
            var state = StartDialing();

            state.Dispose();
            DrainGameThread();

            overlayMock.Verify(x => x.Hide(), Times.AtLeastOnce);
            loadingInterfaceMock.Verify(x => x.HideLoadingScreen(), Times.AtLeastOnce);
        }

        [Fact]
        public void Dispose_AfterTheHandshakeLands_LeavesTheWindowToTheNextState()
        {
            var state = StartDialing();
            state.Handle_NetworkConnected(Payload(new NetworkConnected()));
            DrainGameThread();
            loadingInterfaceMock.Invocations.Clear();
            overlayMock.Invocations.Clear();

            state.Dispose();
            DrainGameThread();

            loadingInterfaceMock.Verify(x => x.HideLoadingScreen(), Times.Never);
            overlayMock.Verify(x => x.Hide(), Times.Never);
        }

        [Fact]
        public void ValidateModulesMethod_Transitions_ValidateModuleState()
        {
            // Arrange
            var state = clientLogic.SetState<MainMenuState>();

            // Act
            state.ValidateModules();

            // Assert
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void Connect_ValidateModuleState()
        {
            // Arrange
            var mainMenuState = clientLogic.SetState<MainMenuState>();

            var payload = new MessagePayload<NetworkConnected>(
                this, new NetworkConnected());

            // Act
            mainMenuState.Handle_NetworkConnected(payload);

            // Assert
            Assert.IsType<ValidateModuleState>(clientLogic.State);
            loadingInterfaceMock.Verify(x => x.ShowLoadingScreen(
                "Connecting to Coop Server",
                "Applying patches..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Connecting to Coop Server",
                "Validating modules..."), Times.Once);
        }

        [Fact]
        public void Disconnect_GoesToMainMenu()
        {
            // Arrange
            var mainMenuState = clientLogic.SetState<MainMenuState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.Disconnect();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Disconnect();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMainMenu();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<MainMenuState>(clientLogic.State);
        }
    }
}
