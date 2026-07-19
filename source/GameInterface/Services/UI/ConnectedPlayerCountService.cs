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
    string FormatEncyclopediaTitle(string baseTitle);
}

/// <inheritdoc cref="IConnectedPlayerCountService"/>
public class ConnectedPlayerCountService : IConnectedPlayerCountService
{
    private const string CountSuffixStart = " (";
    private const string CountSuffixEnd = " online)";

    public int ConnectedPlayers { get; private set; }
    public event Action ConnectedPlayersChanged;

    public void UpdateConnectedPlayers(int connectedPlayers)
    {
        int normalizedCount = Math.Max(0, connectedPlayers);
        if (ConnectedPlayers == normalizedCount) return;

        ConnectedPlayers = normalizedCount;
        ConnectedPlayersChanged?.Invoke();
    }

    public string FormatEncyclopediaTitle(string baseTitle)
    {
        string normalizedTitle = RemoveConnectedPlayerCount(baseTitle);
        return $"{normalizedTitle} ({ConnectedPlayers} online)";
    }

    private static string RemoveConnectedPlayerCount(string title)
    {
        if (string.IsNullOrEmpty(title) || !title.EndsWith(CountSuffixEnd, StringComparison.Ordinal))
            return title;

        int suffixStart = title.LastIndexOf(CountSuffixStart, StringComparison.Ordinal);
        if (suffixStart < 0) return title;

        int countStart = suffixStart + CountSuffixStart.Length;
        int countLength = title.Length - CountSuffixEnd.Length - countStart;
        if (countLength <= 0 || !int.TryParse(title.Substring(countStart, countLength), out _))
            return title;

        return title.Substring(0, suffixStart);
    }
}
