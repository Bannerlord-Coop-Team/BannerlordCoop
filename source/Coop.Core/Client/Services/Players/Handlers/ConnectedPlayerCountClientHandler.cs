using Common;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.UI;

namespace Coop.Core.Client.Services.Players.Handlers;

/// <summary>
/// Applies the authoritative connected-player count on the client game thread.
/// </summary>
internal sealed class ConnectedPlayerCountClientHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IConnectedPlayerCountService connectedPlayerCountService;

    public ConnectedPlayerCountClientHandler(
        IMessageBroker messageBroker,
        IConnectedPlayerCountService connectedPlayerCountService)
    {
        this.messageBroker = messageBroker;
        this.connectedPlayerCountService = connectedPlayerCountService;

        messageBroker.Subscribe<NetworkConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
    }

    private void Handle_ConnectedPlayersChanged(MessagePayload<NetworkConnectedPlayersChanged> payload)
    {
        int connectedPlayers = payload.What.ConnectedPlayers;
        GameThread.RunSafe(
            () => connectedPlayerCountService.UpdateConnectedPlayers(connectedPlayers),
            context: nameof(ConnectedPlayerCountClientHandler));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
    }
}
