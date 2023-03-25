using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Time.Messages;
using LiteNetLib;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        private readonly NetPeer _differentPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;

        public TransferCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(_playerId, StubNetworkMessageBroker);
            _differentPlayer.SetId(_playerId.Id + 1);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic);

            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            _connectionLogic.State.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            _connectionLogic.State = new TransferSaveState(_connectionLogic);

            _connectionLogic.Load();

            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new TransferSaveState(_connectionLogic);

            _connectionLogic.CreateCharacter();
            _connectionLogic.TransferSave();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void CreateCharacterState_EntryEvents()
        {
            // Setup event callbacks
            var networkDisableTimeControlsCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkDisableTimeControls>((payload) =>
            {
                networkDisableTimeControlsCount += 1;
            });

            var pauseAndDisableGameTimeControlsCount = 0;
            StubNetworkMessageBroker.Subscribe<PauseAndDisableGameTimeControls>((payload) =>
            {
                pauseAndDisableGameTimeControlsCount += 1;
            });

            var packageGameSaveDataCount = 0;
            StubNetworkMessageBroker.Subscribe<PackageGameSaveData>((payload) =>
            {
                packageGameSaveDataCount += 1;

                // Verify new transaction id is generated
                Assert.NotEqual(default, payload.What.TransactionID);
            });

            // Trigger state entry
            _connectionLogic.State = new TransferSaveState(_connectionLogic);

            // All events are called exactly once
            Assert.Equal(1, networkDisableTimeControlsCount);
            Assert.Equal(1, pauseAndDisableGameTimeControlsCount);
            Assert.Equal(1, packageGameSaveDataCount);
        }

        [Fact]
        public void GameSaveDataPackaged_ValidTransactionId()
        {
            Guid transactionId = default;
            StubNetworkMessageBroker.Subscribe<PackageGameSaveData>((payload) =>
            {
                transactionId = payload.What.TransactionID;
            });

            _connectionLogic.State = new TransferSaveState(_connectionLogic);

            // Ensure transaction id was generated
            Assert.NotEqual(default, transactionId);

            var networkGameSaveDataRecievedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkGameSaveDataRecieved>((payload) =>
            {
                networkGameSaveDataRecievedCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubMessageBroker.Publish(_playerId, new GameSaveDataPackaged(transactionId, Array.Empty<byte>()));

            Assert.Equal(1, networkGameSaveDataRecievedCount);
            Assert.IsType<LoadingState>(_connectionLogic.State);
        }

        [Fact]
        public void GameSaveDataPackaged_InvalidTransactionId()
        {
            _connectionLogic.State = new TransferSaveState(_connectionLogic);

            var networkGameSaveDataRecievedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkGameSaveDataRecieved>((payload) =>
            {
                networkGameSaveDataRecievedCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubMessageBroker.Publish(_playerId, new GameSaveDataPackaged(Guid.NewGuid(), Array.Empty<byte>()));

            Assert.Equal(0, networkGameSaveDataRecievedCount);
            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }
    }
}
