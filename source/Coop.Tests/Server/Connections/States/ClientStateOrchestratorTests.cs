using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ClientStateOrchestratorTests : CoopTest
    {
        private readonly ClientStateOrchestrator clientStateOrchestrator;
        private readonly string _playerId = Guid.NewGuid().ToString();
        public ClientStateOrchestratorTests(ITestOutputHelper output) : base(output)
        {
            var playerConnectionStates = new PlayerConnectionStatesManager(messageBroker);
            clientStateOrchestrator = new ClientStateOrchestrator(messageBroker, playerConnectionStates);
        }

        [Fact]
        public void PlayerDisconnected_RemovePlayer()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new PlayerDisconnected(_playerId));

            Assert.Empty(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates);
        }

        [Fact]
        public void PlayerResolveCharacter_AddsNewPlayer()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));

            Assert.Single(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates);
            Assert.IsType<ResolveCharacterState>(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerResolvedCharacter_UpdatesPlayerState_LoadingState()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new ResolvedCharacter(_playerId));

            Assert.IsType<LoadingState>(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerJoined_Publishes_PlayerLoading()
        {
            var messagePublished = false;
            messageBroker.Subscribe<PlayerLoading>((_playerId) =>
            {
                messagePublished = true;
            });

            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new ResolvedCharacter(_playerId));

            Assert.True(messagePublished);
        }

        [Fact]
        public void PlayerLoaded_EntersCampaignState()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new ResolvedCharacter(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates.Single().Value.State);
        }

        [Fact]
        public void PlayerEntersMissionState()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new ResolvedCharacter(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));
            messageBroker.Publish(this, new PlayerTransitionMission(_playerId));

            Assert.IsType<MissionState>(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates.Single().Value.State);
        }


        [Fact]
        public void PlayerMissionState_TransitionsCampaignState()
        {
            messageBroker.Publish(this, new ResolveCharacter(_playerId));
            messageBroker.Publish(this, new ResolvedCharacter(_playerId));
            messageBroker.Publish(this, new PlayerLoaded(_playerId));
            messageBroker.Publish(this, new PlayerTransitionMission(_playerId));
            messageBroker.Publish(this, new PlayerTransitionCampaign(_playerId));

            Assert.IsType<CampaignState>(clientStateOrchestrator.PlayerConnectionStates.ConnectionStates.Single().Value.State);
        }
    }
}
