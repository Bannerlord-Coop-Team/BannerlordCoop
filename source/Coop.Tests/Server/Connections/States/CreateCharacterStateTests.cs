using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CreateCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        private readonly NetPeer _differentPlayer = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public CreateCharacterStateTests(ITestOutputHelper output) : base(output)
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
        public void TransferCharacter_TransitionState_TransferCharacterState()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.TransferSave();

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.CreateCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferedHero_ValidPlayerId()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            var registerNewPlayerHeroCount = 0;
            StubNetworkMessageBroker.Subscribe<RegisterNewPlayerHero>((payload) =>
            {
                registerNewPlayerHeroCount += 1;

                // Message should state hero does not exist
                Assert.NotEqual(default, payload.What.TransactionID);
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.ReceiveNetworkEvent(_playerId, new NetworkTransferedHero(Array.Empty<byte>()));

            // A single message is sent
            Assert.Equal(1, registerNewPlayerHeroCount);

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferedHero_InvalidPlayerId()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            var registerNewPlayerHeroCount = 0;
            StubNetworkMessageBroker.Subscribe<RegisterNewPlayerHero>((payload) =>
            {
                registerNewPlayerHeroCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.ReceiveNetworkEvent(_differentPlayer, new NetworkTransferedHero(Array.Empty<byte>()));

            // A single message is sent
            Assert.Equal(0, registerNewPlayerHeroCount);

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NewPlayerHeroRegistered_ValidPlayerId()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            var registerNewPlayerHeroCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkPlayerData>((payload) =>
            {
                registerNewPlayerHeroCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.Publish(_playerId, new NewPlayerHeroRegistered());

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void NewPlayerHeroRegistered_InvalidPlayerId()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            var registerNewPlayerHeroCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkPlayerData>((payload) =>
            {
                registerNewPlayerHeroCount += 1;
            });

            // Publish hero resolved, this would be from game interface
            StubNetworkMessageBroker.ReceiveNetworkEvent(_differentPlayer, new NetworkTransferedHero(Array.Empty<byte>()));

            // A single message is sent
            Assert.Equal(0, registerNewPlayerHeroCount);

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }
    }
}
