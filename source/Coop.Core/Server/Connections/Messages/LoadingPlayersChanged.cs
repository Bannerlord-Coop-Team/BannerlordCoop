using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Published by the <see cref="ConnectionCollection"/> whenever the number of connections in a
/// loading state changes so server handlers can message players while clients are joining.
/// </summary>
internal record LoadingPlayersChanged : IEvent
{
    /// <summary>
    /// The number of connections currently in a loading state.
    /// </summary>
    public int LoadingPlayerCount { get; }

    public LoadingPlayersChanged(int loadingPlayerCount)
    {
        LoadingPlayerCount = loadingPlayerCount;
    }
}
