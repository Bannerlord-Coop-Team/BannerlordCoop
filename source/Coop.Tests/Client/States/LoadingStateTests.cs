using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class LoadingStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

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
            var loadingState = clientLogic.SetState<LoadingState>();

            var payload = new MessagePayload<AllGameObjectsRegistered>(
                this, new AllGameObjectsRegistered());

            // Act
            loadingState.Handle_AllGameObjectsRegistered(payload);

            // Assert
            Assert.IsType<CampaignState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            clientLogic.SetState<LoadingState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            Assert.Equal(1, clientComponent.TestMessageBroker.GetMessageCountFromType<EnterMainMenu>());
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
