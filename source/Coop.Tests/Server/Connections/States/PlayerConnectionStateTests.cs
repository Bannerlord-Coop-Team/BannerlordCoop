using Coop.Core.Server.Connections.States;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class PlayerConnectionStateTests : CoopTest
    {
        private readonly IPlayerConnectionStates _playerConnectionStates;
        private readonly string _playerId = Guid.NewGuid().ToString();

        public PlayerConnectionStateTests(ITestOutputHelper output) : base(output)
        {
            _playerConnectionStates = new PlayerConnectionStates(messageBroker);
        }

        [Fact]
        public void AddNewPlayer_AddsNewConnectionState_ClientJoinCommences()
        {
            _playerConnectionStates.AddNewPlayer(_playerId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.Single(_playerConnectionStates.ConnectionStates);
            Assert.Equal(_playerId, connectionStateKeyPairValue.Key);
            Assert.IsType<JoiningState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void RemovePlayer_RemovesExistingConnectionState()
        {
            _playerConnectionStates.AddNewPlayer(_playerId);

            _playerConnectionStates.RemovePlayer(_playerId);

            Assert.Empty(_playerConnectionStates.ConnectionStates);
        }

        [Fact]
        public void RemovePlayer_NoMatchingId_ShortCircuit()
        {
            var nonExistantId = "nonExistantId";
            _playerConnectionStates.AddNewPlayer(_playerId);

            _playerConnectionStates.RemovePlayer(nonExistantId);
            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.Single(_playerConnectionStates.ConnectionStates);
            Assert.Equal(_playerId, connectionStateKeyPairValue.Key);
            Assert.IsType<JoiningState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void PlayerJoined_ChangesPlayerConnectionState_PlayerLoading()
        {
             _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<LoadingState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void PlayerJoined_NoMatchingPlayerId_ShortCircuits()
        {
            var nonExistantId = "nonExistantId";
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(nonExistantId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<JoiningState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void PlayerLoaded_ChangesPlayerConnectionState_EnterCampaign()
        {
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(_playerId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<CampaignState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void PlayerLoaded_NoMatchingPlayerId_ShortCircuits()
        {
            var nonExistantId = "nonExistantId";
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(nonExistantId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<LoadingState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void EnterCampaign_ChangesPlayerConnectionState_EnterCampaign()
        {
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(_playerId);
            _playerConnectionStates.EnterMission(_playerId);
            _playerConnectionStates.EnterCampaign(_playerId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<CampaignState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void EnterCampaign_NoMatchingPlayerId_ShortCircuits()
        {
            var nonExistantId = "nonExistantId";
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(_playerId);
            _playerConnectionStates.EnterMission(_playerId);
            _playerConnectionStates.EnterCampaign(nonExistantId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<MissionState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void EnterMission_ChangesPlayerConnectionState_EnterMission()
        {
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(_playerId);
            _playerConnectionStates.EnterMission(_playerId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<MissionState>(connectionStateKeyPairValue.Value.State);
        }

        [Fact]
        public void EnterMission_NoMatchingPlayerId_ShortCircuits()
        {
            var nonExistantId = "nonExistantId";
            _playerConnectionStates.AddNewPlayer(_playerId);
            _playerConnectionStates.PlayerJoined(_playerId);
            _playerConnectionStates.PlayerLoaded(_playerId);
            _playerConnectionStates.EnterMission(nonExistantId);

            var connectionStateKeyPairValue = _playerConnectionStates.ConnectionStates.Single();

            Assert.IsType<CampaignState>(connectionStateKeyPairValue.Value.State);
        }
    }
}
