using Autofac;
using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
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
            initialState.Handle_GameLoaded(payload);

            // Assert
            Assert.IsType<ServerRunningState>(serverLogic.State);
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
