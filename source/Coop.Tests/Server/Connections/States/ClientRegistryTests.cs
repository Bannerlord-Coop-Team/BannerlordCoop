using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time.Messages;
using LiteNetLib;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ClientRegistryTests : CoopTest
    {
        private readonly ClientRegistry clientStateOrchestrator;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        private readonly NetPeer _differentPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public ClientRegistryTests(ITestOutputHelper output) : base(output)
        {
            clientStateOrchestrator = new ClientRegistry(NetworkMessageBroker);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            clientStateOrchestrator.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new PlayerDisconnected(_playerId, default(DisconnectInfo)));

            Assert.Empty(clientStateOrchestrator.ConnectionStates);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));

            Assert.Single(clientStateOrchestrator.ConnectionStates);
            Assert.IsType<ResolveCharacterState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_NoLoaders()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));

            var networkEnableTimeControlMessageCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkEnableTimeControls>((_playerId) =>
            {
                networkEnableTimeControlMessageCount += 1;
            });

            var enableTimeControlMessageCount = 0;
            MessageBroker.Subscribe<EnableGameTimeControls>((_playerId) =>
            {
                enableTimeControlMessageCount += 1;
            });

            MessageBroker.Publish(_playerId, new PlayerCampaignEntered());

            Assert.Equal(1, networkEnableTimeControlMessageCount);
            Assert.Equal(1, enableTimeControlMessageCount);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_WithLoaders()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));

            IConnectionLogic logic = clientStateOrchestrator.ConnectionStates.Single().Value;
            logic.State = new LoadingState(logic);

            var networkEnableTimeControlMessageCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkEnableTimeControls>((_playerId) =>
            {
                networkEnableTimeControlMessageCount += 1;
            });

            var enableTimeControlMessageCount = 0;
            MessageBroker.Subscribe<EnableGameTimeControls>((_playerId) =>
            {
                enableTimeControlMessageCount += 1;
            });

            MessageBroker.Publish(_playerId, new PlayerCampaignEntered());

            Assert.Equal(0, networkEnableTimeControlMessageCount);
            Assert.Equal(0, enableTimeControlMessageCount);
        }
    }
}
