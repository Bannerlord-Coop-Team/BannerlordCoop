using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections
{
    /// <summary>
    /// Handle Client connection state as it pertains to loading in a given player
    /// </summary>
    public interface IClientStateOrchestrator
    {
    }

    /// <inheritdoc cref="IClientStateOrchestrator"/>
    public class ClientStateOrchestrator : IClientStateOrchestrator
    {
        public IDictionary<NetPeer, IConnectionLogic> ConnectionStates { get; private set; } = new Dictionary<NetPeer, IConnectionLogic>();

        private readonly IMessageBroker _messageBroker;

        public ClientStateOrchestrator(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _messageBroker.Subscribe<PlayerConnected>(PlayerJoiningHandler);
            _messageBroker.Subscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Subscribe<CharacterResolved>(PlayerJoinedHandler);
            _messageBroker.Subscribe<PlayerCreatingCharacter>(PlayerCreatingCharacterHandler);
            _messageBroker.Subscribe<PlayerTransferCharacter>(PlayerTransferCharacterHandler);
            _messageBroker.Subscribe<PlayerLoaded>(PlayerLoadedHandler);
            _messageBroker.Subscribe<PlayerTransitionCampaign>(PlayerTransitionsCampaignHandler);
            _messageBroker.Subscribe<PlayerTransitionMission>(PlayerTransitionsMissionHandler);
        }

        ~ClientStateOrchestrator()
        {
            _messageBroker.Unsubscribe<PlayerConnected>(PlayerJoiningHandler);
            _messageBroker.Unsubscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Unsubscribe<CharacterResolved>(PlayerJoinedHandler);
            _messageBroker.Unsubscribe<PlayerCreatingCharacter>(PlayerCreatingCharacterHandler);
            _messageBroker.Unsubscribe<PlayerTransferCharacter>(PlayerTransferCharacterHandler);
            _messageBroker.Unsubscribe<PlayerLoaded>(PlayerLoadedHandler);
            _messageBroker.Unsubscribe<PlayerTransitionCampaign>(PlayerTransitionsCampaignHandler);
            _messageBroker.Unsubscribe<PlayerTransitionMission>(PlayerTransitionsMissionHandler);
        }

        private void PlayerJoiningHandler(MessagePayload<PlayerConnected> obj)
        {
            var playerId = obj.What.PlayerId;
            var connectionLogic = new ConnectionLogic();
            ConnectionStates.Add(playerId, connectionLogic);
            connectionLogic.ResolveCharacter();
        }

        private void PlayerDisconnectedHandler(MessagePayload<PlayerDisconnected> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.ContainsKey(playerId))
                return;

            ConnectionStates.Remove(playerId);
        }

        private void PlayerJoinedHandler(MessagePayload<CharacterResolved> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.Load();
            _messageBroker.Publish(this, new PlayerLoading(playerId));
        }

        private void PlayerCreatingCharacterHandler(MessagePayload<PlayerCreatingCharacter> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.CreateCharacter();
        }

        private void PlayerTransferCharacterHandler(MessagePayload<PlayerTransferCharacter> obj)
        {
            var playerId = obj.Who as NetPeer;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.TransferCharacter();
            _messageBroker.Publish(this, new PlayerCharacterTransfered(playerId));
        }

        private void PlayerLoadedHandler(MessagePayload<PlayerLoaded> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterCampaign();
        }

        private void PlayerTransitionsCampaignHandler(MessagePayload<PlayerTransitionCampaign> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterCampaign();
        }

        private void PlayerTransitionsMissionHandler(MessagePayload<PlayerTransitionMission> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterMission();
        }
    }
}
