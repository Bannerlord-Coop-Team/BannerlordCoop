using Autofac;
using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
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
            var network = container.Resolve<TestNetwork>();

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
        public void PlayerDisconnected_LoadingPlayerClearsUnpauseBlock()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            var disconnectPayload = new MessagePayload<PlayerDisconnected>(this, new PlayerDisconnected(playerPeer, default));

            // Act
            clientRegistry.PlayerJoiningHandler(connectPayload);
            clientRegistry.ConnectionStates[playerPeer].SetState<TransferSaveState>();
            Assert.True(clientRegistry.PlayersLoading);
            clientRegistry.PlayerDisconnectedHandler(disconnectPayload);

            // Assert
            Assert.False(clientRegistry.PlayersLoading);
            Assert.Empty(clientRegistry.LoadingPeers);
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
        public void PlayersLoading_BlockedUntilPlayerEntersCampaign()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            clientRegistry.PlayerJoiningHandler(connectPayload);
            var connectionLogic = clientRegistry.ConnectionStates[playerPeer];

            // Assert
            // Character resolution/creation happen before the save snapshot is taken,
            // so time is free to unpause during them.
            Assert.False(clientRegistry.PlayersLoading);
            Assert.Empty(clientRegistry.LoadingPeers);

            connectionLogic.SetState<CreateCharacterState>();
            Assert.False(clientRegistry.PlayersLoading);
            Assert.Empty(clientRegistry.LoadingPeers);

            // The save is packaged in TransferSaveState and consumed through LoadingState,
            // so time must stay locked across that window.
            connectionLogic.SetState<TransferSaveState>();
            Assert.True(clientRegistry.PlayersLoading);
            Assert.Contains(playerPeer, clientRegistry.LoadingPeers);

            connectionLogic.SetState<LoadingState>();
            Assert.True(clientRegistry.PlayersLoading);
            Assert.Contains(playerPeer, clientRegistry.LoadingPeers);

            connectionLogic.SetState<CampaignState>();
            Assert.False(clientRegistry.PlayersLoading);
            Assert.Empty(clientRegistry.LoadingPeers);
        }

        [Fact]
        public void LoadingTransitions_BroadcastLoadingPlayersChangedOnChangeOnly()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            clientRegistry.PlayerJoiningHandler(connectPayload);
            var connectionLogic = clientRegistry.ConnectionStates[playerPeer];
            serverComponent.TestMessageBroker.Messages.Clear();

            // Act & Assert: entering a loading state broadcasts a count of 1.
            connectionLogic.SetState<TransferSaveState>();
            var loadingMessage = Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<LoadingPlayersChanged>());
            Assert.Equal(1, loadingMessage.LoadingPlayerCount);

            // Staying loading (TransferSave -> Loading) does not re-broadcast an unchanged count.
            serverComponent.TestMessageBroker.Messages.Clear();
            connectionLogic.SetState<LoadingState>();
            Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<LoadingPlayersChanged>());

            // Leaving the loading states broadcasts a count of 0.
            connectionLogic.SetState<CampaignState>();
            var clearedMessage = Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<LoadingPlayersChanged>());
            Assert.Equal(0, clearedMessage.LoadingPlayerCount);
        }
    }
}
