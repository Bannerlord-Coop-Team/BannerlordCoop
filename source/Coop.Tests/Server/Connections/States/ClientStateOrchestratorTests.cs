using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.Messages.Incoming;
using Coop.Core.Server.Connections.Messages.Outgoing;
using Coop.Core.Server.Connections.States;
using LiteNetLib;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ClientStateOrchestratorTests : CoopTest
    {
        private readonly ClientRegistry clientStateOrchestrator;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public ClientStateOrchestratorTests(ITestOutputHelper output) : base(output)
        {
            clientStateOrchestrator = new ClientRegistry(MessageBroker);
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new PlayerDisconnected(_playerId, default(DisconnectInfo)));

            Assert.Empty(clientStateOrchestrator.ConnectionStates);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));

            Assert.Single(clientStateOrchestrator.ConnectionStates);
            Assert.IsType<ResolveCharacterState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerCharacterResolved_UpdatesPlayerState_LoadingState()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new CharacterResolved(_playerId));

            Assert.IsType<LoadingState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerJoined_Publishes_PlayerLoading()
        {
            var messagePublished = false;
            MessageBroker.Subscribe<PlayerLoading>((_playerId) =>
            {
                messagePublished = true;
            });

            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new CharacterResolved(_playerId));

            Assert.True(messagePublished);
        }

        [Fact]
        public void PlayerLoaded_EntersCampaignState()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new CharacterResolved(_playerId));
            MessageBroker.Publish(this, new PlayerLoaded(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerEntersMissionState()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new CharacterResolved(_playerId));
            MessageBroker.Publish(this, new PlayerLoaded(_playerId));
            MessageBroker.Publish(this, new PlayerTransitionedToMission(_playerId));

            Assert.IsType<MissionState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }


        [Fact]
        public void PlayerMissionState_TransitionsCampaignState()
        {
            MessageBroker.Publish(this, new PlayerConnected(_playerId));
            MessageBroker.Publish(this, new CharacterResolved(_playerId));
            MessageBroker.Publish(this, new PlayerLoaded(_playerId));
            MessageBroker.Publish(this, new PlayerTransitionedToMission(_playerId));
            MessageBroker.Publish(this, new PlayerTransitionedToCampaign(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }
    }
}
