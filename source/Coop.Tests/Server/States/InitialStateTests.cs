using Common.Messaging;
using Common.Network;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using Moq;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class InitialStateTests : CoopTest
    {
        public InitialStateTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void InitialStateStart()
        {
            // Arrange
            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            Mock<INetwork> coopServer = new Mock<INetwork>();
            IServerState currentState = new InitialServerState(serverLogic.Object, MockMessageBroker);
            serverLogic.SetupSet(x => x.State = It.IsAny<IServerState>()).Callback<IServerState>(value => currentState = value);
            serverLogic.Setup(m => m.Network).Returns(coopServer.Object);

            // Act
            currentState.Start();

            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<LoadDebugGame>(MockMessageBroker.PublishedMessages.First());

            var payload = new MessagePayload<CampaignReady>(null, new CampaignReady());
            var initialState = Assert.IsType<InitialServerState>(currentState);
            initialState.Handle_GameLoaded(payload);

            // Assert
            Assert.IsType<ServerRunningState>(currentState);
        }

        [Fact]
        public void InitialStateStop()
        {
            // Arrange
            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new InitialServerState(serverLogic.Object, MockMessageBroker);
            serverLogic.SetupSet(x => x.State = It.IsAny<IServerState>()).Callback<IServerState>(value => currentState = value);

            // Act
            currentState.Stop();

            // Assert
            Assert.IsType<InitialServerState>(currentState);
        }

        [Fact]
        public void InitialStateDispose()
        {
            // Arrange
            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new InitialServerState(serverLogic.Object, MockMessageBroker);

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            currentState.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }
    }
}
