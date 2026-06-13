using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Registry.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class LoadingStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;
        private readonly Mock<ILoadingInterface> loadingInterfaceMock;

        public LoadingStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
        }

        [Fact]
        public void CampaignLoaded_Transitions_CampaignState()
        {
            // Arrange
            var loadingState = clientLogic.SetState<LoadingState>();

            var payload = new MessagePayload<CampaignReady>(
                this, new CampaignReady());

            // Act
            loadingState.Handle_CampaignLoaded(payload);

            // Assert
            Assert.IsType<CampaignState>(clientLogic.State);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Registering campaign objects..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Applying synced object lifetimes..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Creating remote player heroes..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Registering player control..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Switching to your hero..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Loading Host Campaign",
                "Entering campaign..."), Times.Once);
        }

        [Fact]
        public void Disconnect_GoesToMainMenu()
        {
            // Arrange
            clientLogic.SetState<LoadingState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.Disconnect();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            clientLogic.SetState<LoadingState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.EnterMainMenu();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<LoadingState>(clientLogic.State);
        }
    }
}
