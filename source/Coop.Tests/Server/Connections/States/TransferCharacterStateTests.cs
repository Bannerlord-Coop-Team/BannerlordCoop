using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Moq;
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

            // No CoopSession mock needed: Handle_GameSaveDataPackaged reads CoopSession?.CraftingPlayerData,
            // which is null-safe.

            // Act
            var payload = new MessagePayload<GameSaveDataPackaged>(
                null, new GameSaveDataPackaged(data, campaignId));
            currentState.Handle_GameSaveDataPackaged(payload);

            // Assert — the save data is sent only to the joining peer. Time-control pausing now goes through the
            // mocked ITimeControlInterface (TransferSaveState ctor), so it no longer produces a counted broadcast
            // here; only the directed NetworkGameSaveDataReceived reaches the wire.
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            var castedMessage = Assert.IsType<NetworkGameSaveDataReceived>(message);
            Assert.Equal(data, castedMessage.GameSaveData);
            Assert.Equal(campaignId, castedMessage.CampaignID);

            // GetPeerMessages indexes the backing dictionary directly and would throw for a peer that never
            // received anything, so assert the absence of the key instead.
            Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(differentPeer.Id));
        }
    }
}
