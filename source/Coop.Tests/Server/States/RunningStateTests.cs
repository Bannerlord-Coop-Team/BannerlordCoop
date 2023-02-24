using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using Coop.Tests.Stubs;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class RunningStateTests : CoopTest
    {
        public RunningStateTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void RunningStateStart()
        {

            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new ServerRunningState(serverLogic.Object, MessageBroker);
            serverLogic.SetupSet(x => x.State = currentState);

            currentState.Start();

            Assert.IsType<ServerRunningState>(currentState);
        }


        [Fact]
        public void RunningStateStop()
        {

            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            Mock<ICoopServer> coopServer = new Mock<ICoopServer>();
            IServerState currentState = new ServerRunningState(serverLogic.Object, MessageBroker);
            serverLogic.SetupSet(x => x.State = It.IsAny<IServerState>()).Callback<IServerState>(value => currentState = value);
            serverLogic.Setup(m => m.NetworkServer).Returns(coopServer.Object);

            MessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                MessageBroker.Publish(null, new MainMenuEntered());
            });

            currentState.Stop();

            Assert.IsType<InitialServerState>(currentState);
        }

        [Fact]
        public void RunningStateDispose()
        {

            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new ServerRunningState(serverLogic.Object, MessageBroker);

            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            currentState.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
        }
    }
}
