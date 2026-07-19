using System;

namespace GameInterface.Services.UI;

/// <summary>
/// Provides the server's connected-player count to client UI consumers.
/// </summary>
public interface IConnectedPlayerCountService : IGameAbstraction
{
    int ConnectedPlayers { get; }
    event Action ConnectedPlayersChanged;
    void UpdateConnectedPlayers(int connectedPlayers);
    string FormatEncyclopediaTitle(string title);
}

/// <inheritdoc cref="IConnectedPlayerCountService"/>
public class ConnectedPlayerCountService : IConnectedPlayerCountService
{
    public int ConnectedPlayers { get; private set; }
    public event Action ConnectedPlayersChanged;

    public void UpdateConnectedPlayers(int connectedPlayers)
    {
        int normalizedCount = Math.Max(0, connectedPlayers);
        if (ConnectedPlayers == normalizedCount) return;

        ConnectedPlayers = normalizedCount;
        ConnectedPlayersChanged?.Invoke();
    }

    public string FormatEncyclopediaTitle(string title)
    {
        string suffix = $" ({ConnectedPlayers} online)";
        return title.EndsWith(suffix, StringComparison.Ordinal) ? title : title + suffix;
    }
}
