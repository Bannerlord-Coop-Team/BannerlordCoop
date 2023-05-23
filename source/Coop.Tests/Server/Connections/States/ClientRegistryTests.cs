using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
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
        private readonly ClientRegistry clientRegistry;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPlayer;
        public ClientRegistryTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPlayer = MockNetwork.CreatePeer();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<ConnectionLogic>().As<IConnectionLogic>();
            builder.RegisterInstance(MockMessageBroker).As<IMessageBroker>();
            builder.RegisterInstance(MockNetwork).As<INetwork>();

            IContainer container = builder.Build();

            clientRegistry = new ClientRegistry(MockMessageBroker, MockNetwork);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            clientRegistry.Dispose();

            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            var disconnectPayload = new MessagePayload<PlayerDisconnected>(this, new PlayerDisconnected(playerPeer, default));

            // Act
            clientRegistry.PlayerJoiningHandler(connectPayload);
            Assert.Single(clientRegistry.ConnectionStates);
            clientRegistry.PlayerDisconnectedHandler(disconnectPayload);

            // Assert
            Assert.Empty(clientRegistry.ConnectionStates);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            
            // Act
            clientRegistry.PlayerJoiningHandler(connectPayload);

            // Assert
            var connectionState = Assert.Single(clientRegistry.ConnectionStates).Value;
            Assert.IsType<ResolveCharacterState>(connectionState.State);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_NoLoaders()
        {
            // Arrange
            var payload = new MessagePayload<PlayerCampaignEntered>(
                this, new PlayerCampaignEntered());

            // Act
            clientRegistry.PlayerCampaignEnteredHandler(payload);

            // Assert
            Assert.NotEmpty(MockNetwork.Peers);
            foreach (var peer in MockNetwork.Peers)
            {
                var networkMessage = Assert.Single(MockNetwork.GetPeerMessages(peer));
                Assert.IsType<NetworkEnableTimeControls>(networkMessage);
            }

            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnableGameTimeControls>(message);
        }

        [Fact]
        public void EnableTimeControls_PublishesEvents_WithLoaders()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            clientRegistry.PlayerJoiningHandler(connectPayload);

            IConnectionLogic logic = clientRegistry.ConnectionStates.Single().Value;
            logic.State = new LoadingState(logic);

            var payload = new MessagePayload<PlayerCampaignEntered>(
                this, new PlayerCampaignEntered());

            // Act
            clientRegistry.PlayerCampaignEnteredHandler(payload);

            // Assert
            Assert.NotEmpty(MockNetwork.Peers);
            Assert.Empty(MockNetwork.SentNetworkMessages);

            Assert.Empty(MockMessageBroker.PublishedMessages);
        }
    }
}
