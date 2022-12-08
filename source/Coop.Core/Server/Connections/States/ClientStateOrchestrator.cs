using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections.States
{
    /// <summary>
    /// Handle Client connection state as it pertains to loading in a given player
    /// </summary>
    public interface IClientStateOrchestrator
    {
    }

    public class ClientStateOrchestrator : IClientStateOrchestrator
    {
        public IDictionary<string, IConnectionLogic> ConnectionStates { get; private set; } = new Dictionary<string, IConnectionLogic>();

        private readonly IMessageBroker _messageBroker;

        public ClientStateOrchestrator(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _messageBroker.Subscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Subscribe<ResolveCharacter>(PlayerJoiningHandler);
            _messageBroker.Subscribe<ResolvedCharacter>(PlayerJoinedHandler);
            _messageBroker.Subscribe<PlayerCreatingCharacter>(PlayerCreatingCharacterHandler);
            _messageBroker.Subscribe<PlayerTransferCharacter>(PlayerTransferCharacterHandler);
            _messageBroker.Subscribe<PlayerLoaded>(PlayerLoadedHandler);
            _messageBroker.Subscribe<PlayerTransitionCampaign>(PlayerTransitionsCampaignHandler);
            _messageBroker.Subscribe<PlayerTransitionMission>(PlayerTransitionsMissionHandler);
        }

        private void PlayerJoiningHandler(MessagePayload<ResolveCharacter> obj)
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

        private void PlayerJoinedHandler(MessagePayload<ResolvedCharacter> obj)
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
            _messageBroker.Publish(this, new PlayerCreatingCharacter(playerId));
        }

        private void PlayerTransferCharacterHandler(MessagePayload<PlayerTransferCharacter> obj)
        {
            var playerId = obj.What.PlayerId;
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.TransferCharacter();
            _messageBroker.Publish(this, new PlayerTransferCharacter(playerId));
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
