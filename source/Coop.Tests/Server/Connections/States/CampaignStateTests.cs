using Coop.Core.Server.Connections.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CampaignStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;

        public CampaignStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(messageBroker);
        }

        [Fact]
        public void EnterMissionMethod_TransitionState_MissionState()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic, messageBroker);

            _connectionLogic.EnterMission();

            Assert.IsType<MissionState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic, messageBroker);

            _connectionLogic.ResolveCharacter();
            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }
    }
}
