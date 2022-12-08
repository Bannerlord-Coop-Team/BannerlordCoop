using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CreateCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public CreateCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic();
        }

        [Fact]
        public void TransferCharacter_TransitionState_TransferCharacterState()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.TransferCharacter();

            Assert.IsType<TransferCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.CreateCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }
    }
}
