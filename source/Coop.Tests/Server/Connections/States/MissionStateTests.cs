using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using LiteNetLib;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class MissionStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        private readonly NetPeer _differentPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public MissionStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(_playerId, NetworkMessageBroker);
            _differentPlayer.SetId(_playerId.Id + 1);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic);

            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            _connectionLogic.State.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
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

            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferSave();
            _connectionLogic.Load();
            _connectionLogic.EnterMission();

            Assert.IsType<MissionState>(_connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_ValidPlayerId()
        {
            _connectionLogic.State = new MissionState(_connectionLogic);

            // Publish hero resolved, this would be from game interface
            NetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkPlayerCampaignEntered());

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            _connectionLogic.State = new MissionState(_connectionLogic);

            // Publish hero resolved, this would be from game interface
            NetworkMessageBroker.ReceiveNetworkEvent(_differentPlayer, new NetworkPlayerCampaignEntered());

            Assert.IsType<MissionState>(_connectionLogic.State);
        }
    }
}
