using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;

        public TransferCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPeer = MockNetwork.CreatePeer();
            connectionLogic = new ConnectionLogic(playerPeer, MockMessageBroker, MockNetwork);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            connectionLogic.State = new CampaignState(connectionLogic);

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            connectionLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.State = new TransferSaveState(connectionLogic);

            // Act
            connectionLogic.Load();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.State = new TransferSaveState(connectionLogic);

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void CreateCharacterState_EntryEvents()
        {
            // Act
            connectionLogic.State = new TransferSaveState(connectionLogic);

            // Assert
            Assert.NotEmpty(MockNetwork.SentNetworkMessages);

            foreach(var peer in MockNetwork.Peers)
            {
                var message = Assert.Single(MockNetwork.GetPeerMessages(peer));
                Assert.IsType<NetworkDisableTimeControls>(message);
            }

            Assert.IsType<PauseAndDisableGameTimeControls>(MockMessageBroker.PublishedMessages[0]);
            Assert.IsType<PackageGameSaveData>(MockMessageBroker.PublishedMessages[1]);
        }

        [Fact]
        public void GameSaveDataPackaged_ValidTransactionId()
        {
            // Arrange
            var currentState = new TransferSaveState(connectionLogic);
            connectionLogic.State = currentState;

            byte[] data = new byte[1];
            string campaignId = "12345";

            // Act
            var payload = new MessagePayload<GameSaveDataPackaged>(
                null, new GameSaveDataPackaged(data, campaignId));
            currentState.Handle_GameSaveDataPackaged(payload);

            // Assert
            Assert.Equal(2, MockNetwork.GetPeerMessages(playerPeer).Count());
            var message = MockNetwork.GetPeerMessages(playerPeer).Last();
            var castedMessage = Assert.IsType<NetworkGameSaveDataReceived>(message);
            Assert.Equal(data, castedMessage.GameSaveData);
            Assert.Equal(campaignId, castedMessage.CampaignID);

            Assert.Single(MockNetwork.GetPeerMessages(differentPeer));
        }
    }
}
