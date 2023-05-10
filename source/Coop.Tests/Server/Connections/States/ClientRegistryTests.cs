using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
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
            clientStateOrchestrator = new ClientRegistry(StubNetworkMessageBroker);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            clientStateOrchestrator.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            StubMessageBroker.Publish(this, new PlayerConnected(_playerId));
            StubMessageBroker.Publish(this, new PlayerDisconnected(_playerId, default(DisconnectInfo)));

            Assert.Empty(clientStateOrchestrator.ConnectionStates);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            StubMessageBroker.Publish(this, new PlayerConnected(_playerId));

            Assert.Single(clientStateOrchestrator.ConnectionStates);
            Assert.IsType<ResolveCharacterState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_NoLoaders()
        {
            StubMessageBroker.Publish(this, new PlayerConnected(_playerId));

            var networkEnableTimeControlMessageCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkEnableTimeControls>((_playerId) =>
            {
                networkEnableTimeControlMessageCount += 1;
            });

            var enableTimeControlMessageCount = 0;
            StubMessageBroker.Subscribe<EnableGameTimeControls>((_playerId) =>
            {
                enableTimeControlMessageCount += 1;
            });

            StubMessageBroker.Publish(_playerId, new PlayerCampaignEntered());

            Assert.Equal(1, networkEnableTimeControlMessageCount);
            Assert.Equal(1, enableTimeControlMessageCount);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_WithLoaders()
        {
            StubMessageBroker.Publish(this, new PlayerConnected(_playerId));

            IConnectionLogic logic = clientStateOrchestrator.ConnectionStates.Single().Value;
            logic.State = new LoadingState(logic);

            var networkEnableTimeControlMessageCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkEnableTimeControls>((_playerId) =>
            {
                networkEnableTimeControlMessageCount += 1;
            });

            var enableTimeControlMessageCount = 0;
            StubMessageBroker.Subscribe<EnableGameTimeControls>((_playerId) =>
            {
                enableTimeControlMessageCount += 1;
            });

            StubMessageBroker.Publish(_playerId, new PlayerCampaignEntered());

            Assert.Equal(0, networkEnableTimeControlMessageCount);
            Assert.Equal(0, enableTimeControlMessageCount);
        }
    }
}
