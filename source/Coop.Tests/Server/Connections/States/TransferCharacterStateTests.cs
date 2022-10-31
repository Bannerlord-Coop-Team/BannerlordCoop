using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public TransferCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(messageBroker);
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            _connectionLogic.State = new TransferCharacterState(_connectionLogic, messageBroker);

            _connectionLogic.Load();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new TransferCharacterState(_connectionLogic, messageBroker);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferCharacter();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<TransferCharacterState>(_connectionLogic.State);
        }
    }
}
