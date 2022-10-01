using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections.States
{
    public interface IClientStateOrchestrator
    {
    }

    public class ClientStateOrchestrator : IClientStateOrchestrator
    {
        public IPlayerConnectionStates PlayerConnectionStates { get; private set; }

        private readonly IMessageBroker _messageBroker;

        public ClientStateOrchestrator(IMessageBroker messageBroker, IPlayerConnectionStates playerConnectionStates)
        {
            _messageBroker = messageBroker;
            PlayerConnectionStates = playerConnectionStates;

            _messageBroker.Subscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Subscribe<PlayerJoining>(PlayerJoiningHandler);
            _messageBroker.Subscribe<PlayerJoined>(PlayerJoinedHandler);
            _messageBroker.Subscribe<PlayerLoaded>(PlayerLoadedHandler);
            _messageBroker.Subscribe<PlayerTransitionCampaign>(PlayerTransitionsCampaignHandler);
            _messageBroker.Subscribe<PlayerTransitionMission>(PlayerTransitionsMissionHandler);
        }

        private void PlayerDisconnectedHandler(MessagePayload<PlayerDisconnected> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.RemovePlayer(playerId);
        }

        private void PlayerJoiningHandler(MessagePayload<PlayerJoining> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.AddNewPlayer(playerId);
        }

        private void PlayerJoinedHandler(MessagePayload<PlayerJoined> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.PlayerJoined(playerId);
            _messageBroker.Publish(this, new PlayerLoading(playerId));
        }

        private void PlayerLoadedHandler(MessagePayload<PlayerLoaded> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.PlayerLoaded(playerId);
        }

        private void PlayerTransitionsCampaignHandler(MessagePayload<PlayerTransitionCampaign> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.EnterCampaign(playerId);
        }

        private void PlayerTransitionsMissionHandler(MessagePayload<PlayerTransitionMission> obj)
        {
            var playerId = obj.What.PlayerId;
            PlayerConnectionStates.EnterMission(playerId);
        }
    }
}
