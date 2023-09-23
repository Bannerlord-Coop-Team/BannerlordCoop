using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class LoadingStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;

        public LoadingStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void CampaignLoaded_Transitions_CampaignState()
        {
            // Arrange
            var loadingState = new LoadingState(clientLogic);
            clientLogic.State = loadingState;

            var payload = new MessagePayload<CampaignReady>(
                this, new CampaignReady());

            // Act
            loadingState.Handle_CampaignLoaded(payload);

            // Assert
            Assert.IsType<CampaignState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            clientLogic.State = new LoadingState(clientLogic);

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            clientLogic.State = new LoadingState(clientLogic);

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
