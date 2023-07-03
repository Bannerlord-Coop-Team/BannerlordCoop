using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Manages client connections while one or more connections is receiving the game state
/// through a save transfer
/// </summary>
public interface IClientRegistry : IDisposable
{
}

/// <inheritdoc cref="IClientRegistry"/>
public class ClientRegistry : IClientRegistry
{
    internal IDictionary<NetPeer, IConnectionLogic> ConnectionStates { get; private set; } = new Dictionary<NetPeer, IConnectionLogic>();

    private readonly IMessageBroker _messageBroker;
    private readonly INetwork _network;

    public ClientRegistry(IMessageBroker messageBroker, INetwork network)
    {
        _messageBroker = messageBroker;
        _network = network;
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

    internal void PlayerJoiningHandler(MessagePayload<PlayerConnected> obj)
    {
        var playerId = obj.What.PlayerId;
        var connectionLogic = new ConnectionLogic(playerId, _messageBroker, _network);
        ConnectionStates.Add(playerId, connectionLogic);
    }

    internal void PlayerDisconnectedHandler(MessagePayload<PlayerDisconnected> obj)
    {
        var playerId = obj.What.PlayerId;
        
        if(ConnectionStates.TryGetValue(playerId, out IConnectionLogic logic))
        {
            ConnectionStates.Remove(playerId);
            logic.Dispose();
        }
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<PlayerCampaignEntered> obj)
    {
        EnableTimeControls();
    }

    private static HashSet<Type> loadingStates = new HashSet<Type>
    {
        typeof(TransferSaveState),
        typeof(LoadingState),
    };
    private void EnableTimeControls()
    {
        // Only re-enable if all connections are finished loading
        //if (ConnectionStates.Any(state => loadingStates.Contains(state.Value.State.GetType()))) return;

        _network.SendAll(new NetworkEnableTimeControls());
        _messageBroker.Publish(this, new EnableGameTimeControls());
    }
}
