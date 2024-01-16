using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server;
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
    public class TransferCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public TransferCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.SetState<TransferSaveState>();

            // Act
            connectionLogic.Load();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<TransferSaveState>();

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void GameSaveDataPackaged_ValidTransactionId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<TransferSaveState>();

            byte[] data = new byte[1];
            string campaignId = "12345";

            // Act
            var payload = new MessagePayload<GameSaveDataPackaged>(
                null, new GameSaveDataPackaged(data, campaignId));
            currentState.Handle_GameSaveDataPackaged(payload);

            // Assert
            Assert.Equal(2, serverComponent.TestNetwork.GetPeerMessages(playerPeer).Count());
            var message = serverComponent.TestNetwork.GetPeerMessages(playerPeer).Last();
            var castedMessage = Assert.IsType<NetworkGameSaveDataReceived>(message);
            Assert.Equal(data, castedMessage.GameSaveData);
            Assert.Equal(campaignId, castedMessage.CampaignID);

            Assert.Single(serverComponent.TestNetwork.GetPeerMessages(differentPeer));
        }
    }
}
