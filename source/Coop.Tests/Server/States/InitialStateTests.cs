using Autofac;
using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class InitialStateTests
    {
        private readonly ServerTestComponent serverComponent;

        public InitialStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;
        }

        [Fact]
        public void InitialStateStart()
        {
            // Arrange
            IServerLogic serverLogic = serverComponent.Container.Resolve<IServerLogic>();

            // Act
            serverLogic.State.Start();

            var payload = new MessagePayload<CampaignReady>(null, new CampaignReady());
            var initialState = Assert.IsType<InitialServerState>(serverLogic.State);
            initialState.Handle_CampaignReady(payload);

            // Assert
            Assert.IsType<ServerRunningState>(serverLogic.State);
        }

        [Fact]
        public void CampaignReady_SetsLoadingMessages()
        {
            // Arrange
            IServerLogic serverLogic = serverComponent.Container.Resolve<IServerLogic>();
            var loadingInterfaceMock = serverComponent.Container.Resolve<Mock<ILoadingInterface>>();
            var initialState = Assert.IsType<InitialServerState>(serverLogic.State);

            // Act
            initialState.Handle_CampaignReady(new MessagePayload<CampaignReady>(null, new CampaignReady()));

            // Assert
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Hosting Coop Server",
                "Registering campaign objects..."), Times.Once);
            loadingInterfaceMock.Verify(x => x.SetLoadingMessage(
                "Hosting Coop Server",
                "Applying synced object lifetimes..."), Times.Once);
        }

        [Fact]
        public void InitialStateStop()
        {
            // Arrange
            IServerLogic serverLogic = serverComponent.Container.Resolve<IServerLogic>();

            // Act
            serverLogic.State.Stop();

            // Assert
            Assert.IsType<InitialServerState>(serverLogic.State);
        }
    }
}
