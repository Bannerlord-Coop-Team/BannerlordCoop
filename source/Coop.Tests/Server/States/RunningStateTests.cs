using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface.Services.GameState.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class RunningStateTests : CoopTest
    {
        private readonly IServerLogic serverLogic;
        public RunningStateTests(ITestOutputHelper output) : base(output)
        {
            serverLogic = new ServerLogic(MockMessageBroker, MockNetwork);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            var currentState = new ServerRunningState(serverLogic, MockMessageBroker);
            serverLogic.State = currentState;

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            currentState.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void Stop_Publishes_EnterMainMenu()
        {
            // Arrange
            var currentState = new ServerRunningState(serverLogic, MockMessageBroker);

            // Act
            currentState.Stop();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void MainMenuEntered_Transitions_InitialServerState()
        {
            // Arrange
            var currentState = new ServerRunningState(serverLogic, MockMessageBroker);
            serverLogic.State = currentState;

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
            var currentState = new ServerRunningState(serverLogic, MockMessageBroker);
            serverLogic.State = currentState;

            // Act
            ((IServerState)serverLogic).Start();
            
            // Assert
            Assert.IsType<ServerRunningState>(currentState);
        }
    }
}
