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
    public class ConnectionCollectionTests
    {
        private readonly ConnectionCollection connectionCollection;
        private readonly NetPeer playerPeer;
        private readonly ServerTestComponent serverComponent;

        public ConnectionCollectionTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;
            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();

            connectionCollection = container.Resolve<ConnectionCollection>();
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            var disconnectPayload = new MessagePayload<PlayerDisconnected>(this, new PlayerDisconnected(playerPeer, default));

            // Act
            connectionCollection.PlayerJoiningHandler(connectPayload);
            Assert.Single(connectionCollection.ConnectionStates);
            connectionCollection.PlayerDisconnectedHandler(disconnectPayload);

            // Assert
            Assert.Empty(connectionCollection.ConnectionStates);
        }

        [Fact]
        public void PlayerDisconnected_LoadingPlayerClearsUnpauseBlock()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            var disconnectPayload = new MessagePayload<PlayerDisconnected>(this, new PlayerDisconnected(playerPeer, default));

            // Act
            connectionCollection.PlayerJoiningHandler(connectPayload);
            connectionCollection.ConnectionStates[playerPeer].SetState<TransferSaveState>();
            Assert.NotEmpty(connectionCollection.LoadingPeers);
            connectionCollection.PlayerDisconnectedHandler(disconnectPayload);

            // Assert
            Assert.Empty(connectionCollection.LoadingPeers);
            Assert.Empty(connectionCollection.LoadingPeers);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            
            // Act
            connectionCollection.PlayerJoiningHandler(connectPayload);

            // Assert
            var connectionState = Assert.Single(connectionCollection.ConnectionStates).Value;
            Assert.IsType<ResolveCharacterState>(connectionState.State);
        }

        [Fact]
        public void PlayersLoading_BlockedUntilPlayerEntersCampaign()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            connectionCollection.PlayerJoiningHandler(connectPayload);
            var connectionLogic = connectionCollection.ConnectionStates[playerPeer];

            // Assert
            // Character resolution/creation happen before the save snapshot is taken,
            // so time is free to unpause during them.
            Assert.Empty(connectionCollection.LoadingPeers);

            connectionLogic.SetState<CreateCharacterState>();
            Assert.Empty(connectionCollection.LoadingPeers);

            // The save is packaged in TransferSaveState and consumed through LoadingState,
            // so time must stay locked across that window.
            connectionLogic.SetState<TransferSaveState>();
            Assert.Contains(connectionCollection.LoadingPeers, logic => logic.Peer == playerPeer);

            connectionLogic.SetState<LoadingState>();
            Assert.Contains(connectionCollection.LoadingPeers, logic => logic.Peer == playerPeer);

            connectionLogic.SetState<CampaignState>();
            Assert.Empty(connectionCollection.LoadingPeers);
        }

        [Fact]
        public void LoadingTransitions_BroadcastLoadingPlayersChangedOnChangeOnly()
        {
            // Arrange
            var connectPayload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(playerPeer));
            connectionCollection.PlayerJoiningHandler(connectPayload);
            var connectionLogic = connectionCollection.ConnectionStates[playerPeer];
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
