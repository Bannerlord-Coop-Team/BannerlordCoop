using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public ResolveCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            _connectionLogic = new ConnectionLogic(_playerId, StubNetworkMessageBroker);
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
        public void CreateCharacterMethod_TransitionState_CreateCharacterState()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.CreateCharacter();

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void TransferSaveMethod_TransitionState_TransferSaveState()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.TransferSave();

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            _connectionLogic.Load();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NetworkClientValidate_ValidPlayerId()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var resolveDebugHeroCount = 0;
            StubMessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                resolveDebugHeroCount += 1;
            });

            StubNetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkClientValidate());

            // A single message is sent
            Assert.Equal(1, resolveDebugHeroCount);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            var wrongPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var resolveDebugHeroCount = 0;
            StubMessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                resolveDebugHeroCount += 1;
            });

            StubNetworkMessageBroker.ReceiveNetworkEvent(wrongPlayer, new NetworkClientValidate());

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, resolveDebugHeroCount);
        }

        [Fact]
        public void ResolveHero_ValidTransactionId()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            // Generate transaction id
            Guid transactionId = default;
            StubNetworkMessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                transactionId = payload.What.TransactionID;
            });

            StubNetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkClientValidate());

            // Ensure a transaction id was generated
            Assert.NotEqual(default, transactionId);

            var networkClientValidatedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((msg) =>
            {
                networkClientValidatedCount += 1;

                // Message should state hero exists
                Assert.True(msg.What.HeroExists);
            });

            StubMessageBroker.Publish(this, new HeroResolved(transactionId, Guid.Empty));

            // A single message is sent
            Assert.Equal(1, networkClientValidatedCount);

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void ResolveHero_InvalidTransactionId()
        {
            var wrongPlayerId = _playerId.Id + 1;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;
            });

            StubMessageBroker.Publish(this, new HeroResolved(Guid.NewGuid(), Guid.Empty));

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, networkClientValidatedCount);

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void HeroNotFound_ValidTransactionId()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            // Generate transaction id
            Guid transactionId = default;
            StubNetworkMessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                transactionId = payload.What.TransactionID;
            });

            StubNetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkClientValidate());

            // Ensure a transaction id was generated
            Assert.NotEqual(default, transactionId);

            var networkClientValidatedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;

                // Message should state hero does not exist
                Assert.False(payload.What.HeroExists);
            });

            // Publish hero resolved, this would be from game interface
            StubMessageBroker.Publish(_playerId, new ResolveHeroNotFound(transactionId));

            // A single message is sent
            Assert.Equal(1, networkClientValidatedCount);

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void HeroNotFound_InvalidTransactionId()
        {
            var wrongPlayerId = _playerId.Id + 1;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubMessageBroker.Publish(this, new ResolveHeroNotFound(Guid.NewGuid()));

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, networkClientValidatedCount);

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }
    }
}
