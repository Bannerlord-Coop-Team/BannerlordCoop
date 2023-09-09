using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ClientRegistryTests
    {
        private readonly ClientRegistry clientRegistry;
        private readonly NetPeer playerPeer;
        private readonly ServerTestComponent serverComponent;

        private MockMessageBroker MockMessageBroker => serverComponent.MockMessageBroker;
        private MockNetwork MockNetwork => serverComponent.MockNetwork;

        public ClientRegistryTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;
            var network = container.Resolve<MockNetwork>();

            playerPeer = network.CreatePeer();

            clientRegistry = container.Resolve<ClientRegistry>();
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
                var networkMessage = Assert.Single(serverComponent.MockNetwork.GetPeerMessages(peer));
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
            logic.SetState<LoadingState>();

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
