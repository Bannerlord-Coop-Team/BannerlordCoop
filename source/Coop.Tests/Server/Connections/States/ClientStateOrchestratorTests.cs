using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
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
        private readonly ClientStateOrchestrator clientStateOrchestrator;
        private readonly NetPeer _playerId = FormatterServices.GetUninitializedObject(typeof(NetPeer)) as NetPeer;
        public ClientStateOrchestratorTests(ITestOutputHelper output) : base(output)
        {
            clientStateOrchestrator = new ClientStateOrchestrator(messageBroker);
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new PlayerDisconnected(_playerId, default(DisconnectInfo)));

            Assert.Empty(clientStateOrchestrator.ConnectionStates);
        }

        [Fact]
        public void PlayerPlayerConnected_AddsNewPlayer()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));

            Assert.Single(clientStateOrchestrator.ConnectionStates);
            Assert.IsType<ResolveCharacterState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerCharacterResolved_UpdatesPlayerState_LoadingState()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new CharacterResolved(_playerId));

            Assert.IsType<LoadingState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerJoined_Publishes_PlayerLoading()
        {
            var messagePublished = false;
            messageBroker.Subscribe<PlayerLoading>((_playerId) =>
            {
                messagePublished = true;
            });

            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new CharacterResolved(_playerId));

            Assert.True(messagePublished);
        }

        [Fact]
        public void PlayerLoaded_EntersCampaignState()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new CharacterResolved(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerEntersMissionState()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new CharacterResolved(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));
            messageBroker.Publish(this, new PlayerTransitionMission(_playerId));

            Assert.IsType<MissionState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }


        [Fact]
        public void PlayerMissionState_TransitionsCampaignState()
        {
            messageBroker.Publish(this, new PlayerConnected(_playerId));
            messageBroker.Publish(this, new CharacterResolved(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));
            messageBroker.Publish(this, new PlayerTransitionMission(_playerId));
            messageBroker.Publish(this, new PlayerTransitionCampaign(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.ConnectionStates.Single().Value.State);
        }
    }
}
