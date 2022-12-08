using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class LoadingStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public LoadingStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic();
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            _connectionLogic.State = new LoadingState(_connectionLogic);

            _connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new LoadingState(_connectionLogic);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterMission();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }
    }
}
