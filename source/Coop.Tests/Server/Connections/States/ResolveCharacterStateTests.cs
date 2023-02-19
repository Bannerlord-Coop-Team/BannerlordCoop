using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
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
            _connectionLogic = new ConnectionLogic(_playerId, NetworkMessageBroker);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            _connectionLogic.State = new CampaignState(_connectionLogic);

            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            _connectionLogic.State.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
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
            MessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                resolveDebugHeroCount += 1;
            });

            NetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkClientValidate());

            // A single message is sent
            Assert.Equal(1, resolveDebugHeroCount);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            var wrongPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var resolveDebugHeroCount = 0;
            MessageBroker.Subscribe<ResolveDebugHero>((payload) =>
            {
                resolveDebugHeroCount += 1;
            });

            NetworkMessageBroker.ReceiveNetworkEvent(wrongPlayer, new NetworkClientValidate());

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, resolveDebugHeroCount);
        }

        [Fact]
        public void ResolveHero_ValidPlayerId()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((msg) =>
            {
                networkClientValidatedCount += 1;

                // Message should state hero exists
                Assert.True(msg.What.HeroExists);
            });

            MessageBroker.Publish(this, new HeroResolved(_playerId.Id, string.Empty));

            // A single message is sent
            Assert.Equal(1, networkClientValidatedCount);

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void ResolveHero_InvalidPlayerId()
        {
            var wrongPlayerId = _playerId.Id + 1;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;
            });

            MessageBroker.Publish(this, new HeroResolved(wrongPlayerId, string.Empty));

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, networkClientValidatedCount);

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void HeroNotFound_ValidPlayerId()
        {
            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;

                // Message should state hero does not exist
                Assert.False(payload.What.HeroExists);
            });

            // Publish hero resolved, this would be from game interface
            MessageBroker.Publish(_playerId, new ResolveHeroNotFound(_playerId.Id));

            // A single message is sent
            Assert.Equal(1, networkClientValidatedCount);

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void HeroNotFound_InvalidPlayerId()
        {
            var wrongPlayerId = _playerId.Id + 1;

            _connectionLogic.State = new ResolveCharacterState(_connectionLogic);

            var networkClientValidatedCount = 0;
            NetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidated>((payload) =>
            {
                networkClientValidatedCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            MessageBroker.Publish(this, new ResolveHeroNotFound(wrongPlayerId));

            // No message is sent due to this logic is not responsible for this player
            Assert.Equal(0, networkClientValidatedCount);

            Assert.IsType<ResolveCharacterState>(_connectionLogic.State);
        }
    }
}
