using Common.Messaging;
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
            IServerState currentState = new ServerRunningState(serverLogic.Object, messageBroker);
            serverLogic.SetupSet(x => x.State).Callback(value => currentState = value);

            currentState.Start();

            Assert.IsType<ServerRunningState>(currentState);
        }


        [Fact]
        public void RunningStateStop()
        {

            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new ServerRunningState(serverLogic.Object, messageBroker);
            serverLogic.SetupSet(x => x.State).Callback(value => currentState = value);

            messageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                messageBroker.Publish(null, new MainMenuEntered());
            });

            currentState.Stop();

            Assert.IsType<InitialServerState>(currentState);
        }

        [Fact]
        public void RunningStateDispose()
        {

            Mock<IServerLogic> serverLogic = new Mock<IServerLogic>();
            IServerState currentState = new ServerRunningState(serverLogic.Object, messageBroker);

            Assert.NotEqual(0, messageBroker.GetTotalSubscribers());

            currentState.Dispose();

            Assert.Equal(0, messageBroker.GetTotalSubscribers());
        }
    }
}
