using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>Published after the server's connected-player collection changes.</summary>
internal record ConnectedPlayersChanged : IEvent
{
    public int ConnectedPlayers { get; }

    public ConnectedPlayersChanged(int connectedPlayers)
    {
        ConnectedPlayers = connectedPlayers;
    }
}
