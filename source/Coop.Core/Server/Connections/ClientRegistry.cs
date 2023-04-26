using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Time.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Connections
{
    /// <summary>
    /// Handle Client connection state as it pertains to loading in a given player
    /// </summary>
    public interface IClientRegistry : IDisposable
    {
    }

    /// <inheritdoc cref="IClientRegistry"/>
    public class ClientRegistry : IClientRegistry
    {
        public IDictionary<NetPeer, IConnectionLogic> ConnectionStates { get; private set; } = new Dictionary<NetPeer, IConnectionLogic>();

        private readonly INetworkMessageBroker _messageBroker;

        public ClientRegistry(INetworkMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _messageBroker.Subscribe<PlayerConnected>(PlayerJoiningHandler);
            _messageBroker.Subscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Subscribe<PlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<PlayerConnected>(PlayerJoiningHandler);
            _messageBroker.Unsubscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
            _messageBroker.Unsubscribe<PlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        }

        private void PlayerJoiningHandler(MessagePayload<PlayerConnected> obj)
        {
            var playerId = obj.What.PlayerId;
            var connectionLogic = new ConnectionLogic(playerId, _messageBroker);
            ConnectionStates.Add(playerId, connectionLogic);
        }

        private void PlayerDisconnectedHandler(MessagePayload<PlayerDisconnected> obj)
        {
            var playerId = obj.What.PlayerId;
            
            if(ConnectionStates.TryGetValue(playerId, out IConnectionLogic logic))
            {
                ConnectionStates.Remove(playerId);
                logic.Dispose();
            }
        }

        private void PlayerCampaignEnteredHandler(MessagePayload<PlayerCampaignEntered> obj)
        {
            EnableTimeControls();
        }

        private void EnableTimeControls()
        {
            // Only re-enable if all connections are finished loading
            if (ConnectionStates.Any(state => state.Value.State is LoadingState)) return;

            _messageBroker.PublishNetworkEvent(new NetworkEnableTimeControls());
            _messageBroker.Publish(this, new EnableGameTimeControls(Guid.Empty));
        }
    }
}
