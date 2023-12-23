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
    }
}
