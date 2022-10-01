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
            _connectionLogic = new ConnectionLogic(messageBroker);
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            _connectionLogic.State = new MissionState(_connectionLogic, messageBroker);

            _connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new MissionState(_connectionLogic, messageBroker);

            _connectionLogic.Join();
            _connectionLogic.Load();
            _connectionLogic.EnterMission();

            Assert.IsType<MissionState>(_connectionLogic.State);
        }
    }
}
