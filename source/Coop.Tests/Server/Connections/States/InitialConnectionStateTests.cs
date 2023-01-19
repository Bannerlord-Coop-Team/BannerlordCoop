using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class InitialConnectionStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public InitialConnectionStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic();
        }

        [Fact]
        public void ResolveCharacterMethod_TransitionState_JoiningState()
        {
            _connectionLogic.State = new InitialConnectionState(_connectionLogic);

            _connectionLogic.ResolveCharacter();

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new InitialConnectionState(_connectionLogic);

            _connectionLogic.Load();
            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferCharacter();
            _connectionLogic.EnterMission();
            _connectionLogic.EnterCampaign();

            Assert.IsType<InitialConnectionState>(_connectionLogic.State);
        }
    }
}
