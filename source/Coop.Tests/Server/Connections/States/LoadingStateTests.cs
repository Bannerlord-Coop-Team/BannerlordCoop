using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class LoadingStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        private readonly NetPeer _differentPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;

        public LoadingStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(_playerId, StubNetworkMessageBroker);
            _differentPlayer.SetId(_playerId.Id + 1);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic);

            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            _connectionLogic.State.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
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

            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferSave();
            _connectionLogic.Load();
            _connectionLogic.EnterMission();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_ValidPlayerId()
        {
            _connectionLogic.State = new LoadingState(_connectionLogic);

            var playerCampaignEnteredCount = 0;
            StubNetworkMessageBroker.Subscribe<PlayerCampaignEntered>((payload) =>
            {
                playerCampaignEnteredCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkPlayerCampaignEntered());

            // A single message is sent
            Assert.Equal(1, playerCampaignEnteredCount);

            Assert.IsType<CampaignState>(_connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            _connectionLogic.State = new LoadingState(_connectionLogic);

            var playerCampaignEnteredCount = 0;
            StubNetworkMessageBroker.Subscribe<PlayerCampaignEntered>((payload) =>
            {
                playerCampaignEnteredCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.ReceiveNetworkEvent(_differentPlayer, new NetworkPlayerCampaignEntered());

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, playerCampaignEnteredCount);

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }
    }
}
