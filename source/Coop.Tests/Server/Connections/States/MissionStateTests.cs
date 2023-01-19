using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class MissionStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public MissionStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic();
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            _connectionLogic.State = new MissionState(_connectionLogic);

            _connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new MissionState(_connectionLogic);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterMission();

            Assert.IsType<MissionState>(_connectionLogic.State);
        }
    }
}
