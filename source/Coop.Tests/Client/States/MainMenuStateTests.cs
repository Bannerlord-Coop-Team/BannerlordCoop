using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;
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

        public MainMenuStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
            loadingInterfaceMock = container.Resolve<Mock<ILoadingInterface>>();
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
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var mainMenuState = clientLogic.SetState<MainMenuState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EnterMainMenu>());
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
