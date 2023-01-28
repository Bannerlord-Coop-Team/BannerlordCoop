using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public ResolveCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic();
        }

        [Fact]
        public void CreateCharacter_TransitionState_CreateCharacterState()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.CreateCharacter();

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.Load();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.TransferSave();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }
    }
}
