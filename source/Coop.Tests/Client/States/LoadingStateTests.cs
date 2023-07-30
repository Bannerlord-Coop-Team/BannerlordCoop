using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class LoadingStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public LoadingStateTests(ITestOutputHelper output) : base(output)
        {
            clientLogic = ServiceProvider.GetService<IClientLogic>()!;
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            clientLogic.State = new LoadingState(clientLogic);
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            clientLogic.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            clientLogic.State = new LoadingState(clientLogic);

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
            var loadingState = new LoadingState(clientLogic);
            clientLogic.State = loadingState;

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            loadingState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
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

            clientLogic.StartCharacterCreation();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<LoadingState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<LoadingState>(clientLogic.State);
        }
    }
}
