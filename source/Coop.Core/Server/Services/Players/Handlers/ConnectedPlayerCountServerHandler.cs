using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using System;

namespace Coop.Core.Server.Services.Players.Handlers;

/// <summary>
/// Replicates the same connected-player count used by session advertisements.
/// </summary>
internal sealed class ConnectedPlayerCountServerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private int connectedPlayers;

    public ConnectedPlayerCountServerHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_ConnectedPlayersChanged(MessagePayload<ConnectedPlayersChanged> payload)
    {
        connectedPlayers = Math.Max(0, payload.What.ConnectedPlayers);
        network.SendAll(new NetworkConnectedPlayersChanged(connectedPlayers));
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        network.Send(payload.What.playerId, new NetworkConnectedPlayersChanged(connectedPlayers));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }
}
