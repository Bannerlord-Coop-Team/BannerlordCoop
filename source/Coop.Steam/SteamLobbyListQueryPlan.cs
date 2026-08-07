using System.Collections.Generic;
using System.Linq;

namespace Coop.Steam;

internal readonly struct SteamLobbyListQueryResult
{
    public ulong LobbyId { get; }
    public ulong ServerSteamId { get; }

    public SteamLobbyListQueryResult(ulong lobbyId, ulong serverSteamId)
    {
        LobbyId = lobbyId;
        ServerSteamId = serverSteamId;
    }
}

internal readonly struct SteamLobbyListQueryRange
{
    // Public game-server and anonymous-game-server Steam ids are all 17 digits, so Steam's
    // string comparisons preserve their numeric ordering.
    public const ulong MinimumServerSteamId = 85568392920039424UL;
    public const ulong MaximumServerSteamId = 94575592174780415UL;

    public static readonly SteamLobbyListQueryRange Unfiltered =
        new SteamLobbyListQueryRange(MinimumServerSteamId, MaximumServerSteamId, true);

    public ulong Minimum { get; }
    public ulong Maximum { get; }
    public bool IsUnfiltered { get; }

    public SteamLobbyListQueryRange(ulong minimum, ulong maximum)
        : this(minimum, maximum, false)
    {
    }

    private SteamLobbyListQueryRange(ulong minimum, ulong maximum, bool isUnfiltered)
    {
        Minimum = minimum;
        Maximum = maximum;
        IsUnfiltered = isUnfiltered;
    }

    public bool Contains(ulong value)
    {
        return IsUnfiltered || (value >= Minimum && value <= Maximum);
    }

    public static bool IsServerSteamId(ulong value)
    {
        return value >= MinimumServerSteamId && value <= MaximumServerSteamId;
    }
}

/// <summary>Collects lobby ids while recursively splitting saturated Steam result ranges.</summary>
internal sealed class SteamLobbyListQueryPlan
{
    public const int MaxResultsPerQuery = 50;

    private readonly Stack<SteamLobbyListQueryRange> pendingRanges = new();
    private readonly List<ulong> results = new();
    private readonly HashSet<ulong> seenLobbyIds = new();

    public IReadOnlyList<ulong> Results => results;
    public bool WasTruncated { get; private set; }

    public SteamLobbyListQueryPlan()
    {
        pendingRanges.Push(SteamLobbyListQueryRange.Unfiltered);
    }

    public bool TryGetNext(out SteamLobbyListQueryRange range)
    {
        if (pendingRanges.Count == 0)
        {
            range = default;
            return false;
        }

        range = pendingRanges.Pop();
        return true;
    }

    public void AddResults(
        SteamLobbyListQueryRange range,
        IReadOnlyList<SteamLobbyListQueryResult> lobbyList)
    {
        if (lobbyList == null) return;

        foreach (var lobby in lobbyList)
        {
            if (lobby.LobbyId != 0 && seenLobbyIds.Add(lobby.LobbyId)) results.Add(lobby.LobbyId);
        }

        if (lobbyList.Count < MaxResultsPerQuery) return;

        var serverSteamIds = lobbyList
            .Select(lobby => lobby.ServerSteamId)
            .Where(SteamLobbyListQueryRange.IsServerSteamId)
            .Where(range.Contains)
            .Distinct()
            .OrderBy(serverSteamId => serverSteamId)
            .ToArray();
        if (serverSteamIds.Length < 2)
        {
            WasTruncated = true;
            return;
        }

        ulong lowerMaximum = serverSteamIds[(serverSteamIds.Length / 2) - 1];
        ulong minimum = range.IsUnfiltered
            ? SteamLobbyListQueryRange.MinimumServerSteamId
            : range.Minimum;
        ulong maximum = range.IsUnfiltered
            ? SteamLobbyListQueryRange.MaximumServerSteamId
            : range.Maximum;
        pendingRanges.Push(new SteamLobbyListQueryRange(lowerMaximum + 1, maximum));
        pendingRanges.Push(new SteamLobbyListQueryRange(minimum, lowerMaximum));
    }
}
