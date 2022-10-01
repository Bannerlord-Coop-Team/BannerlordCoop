using Common.Messaging;
using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class JoiningStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public JoiningStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(messageBroker);
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            _connectionLogic.State = new JoiningState(_connectionLogic, messageBroker);

            _connectionLogic.Load();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new JoiningState(_connectionLogic, messageBroker);

            _connectionLogic.Join();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<JoiningState>(_connectionLogic.State);
        }
    }
}
