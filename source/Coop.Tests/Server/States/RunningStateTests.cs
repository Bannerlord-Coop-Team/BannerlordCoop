using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class RunningStateTests
    {
        private readonly IServerLogic serverLogic;
        private readonly ServerTestComponent serverComponent;

        public RunningStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            serverLogic = container.Resolve<ServerLogic>();
        }

        [Fact]
        public void Stop_Publishes_EnterMainMenu()
        {
            // Arrange
            var currentState = serverLogic.SetState<ServerRunningState>();

            // Act
            currentState.Stop();

            // Assert
            var message = Assert.Single(serverComponent.TestMessageBroker.Messages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void MainMenuEntered_Transitions_InitialServerState()
        {
            // Arrange
            var currentState = serverLogic.SetState<ServerRunningState>();

            var serverRunningState = Assert.IsType<ServerRunningState>(currentState);

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            serverRunningState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<InitialServerState>(serverLogic.State);
        }
        

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var currentState = serverLogic.SetState<ServerRunningState>();

            // Act
            ((IServerState)serverLogic).Start();
            
            // Assert
            Assert.IsType<ServerRunningState>(currentState);
        }
    }
}
