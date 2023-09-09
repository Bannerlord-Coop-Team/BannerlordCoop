using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;

        public MainMenuStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void ValidateModulesMethod_Transitions_ValidateModuleState()
        {
            // Arrange
            clientLogic.State = new MainMenuState(clientLogic);

            // Act
            clientLogic.State.ValidateModules();

            // Assert
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void Connect_ValidateModuleState()
        {
            // Arrange
            var mainMenuState = new MainMenuState(clientLogic);
            clientLogic.State = mainMenuState;

            var payload = new MessagePayload<NetworkConnected>(
                this, new NetworkConnected());

            // Act
            mainMenuState.Handle_NetworkConnected(payload);

            // Assert
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var mainMenuState = new MainMenuState(clientLogic);
            clientLogic.State = mainMenuState;

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
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
