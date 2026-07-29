using System;
using System.Collections.Generic;

namespace Coop.Steam;

internal readonly struct SteamLobbyListQueryRange
{
    public static readonly SteamLobbyListQueryRange Unfiltered =
        new SteamLobbyListQueryRange(0, int.MaxValue, true);

    public int Minimum { get; }
    public int Maximum { get; }
    public bool IsUnfiltered { get; }

    public SteamLobbyListQueryRange(int minimum, int maximum)
        : this(minimum, maximum, false)
    {
    }

    private SteamLobbyListQueryRange(int minimum, int maximum, bool isUnfiltered)
    {
        Minimum = minimum;
        Maximum = maximum;
        IsUnfiltered = isUnfiltered;
    }

    public bool Contains(int value)
    {
        return IsUnfiltered || (value >= Minimum && value <= Maximum);
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

    public void AddResults(SteamLobbyListQueryRange range, IReadOnlyList<ulong> lobbyIds)
    {
        if (lobbyIds == null) return;

        foreach (var lobbyId in lobbyIds)
        {
            if (lobbyId != 0 && seenLobbyIds.Add(lobbyId)) results.Add(lobbyId);
        }

        if (lobbyIds.Count < MaxResultsPerQuery) return;

        if (range.IsUnfiltered)
        {
            PushSplitRanges(0, int.MaxValue);
            return;
        }

        if (range.Minimum == range.Maximum)
        {
            WasTruncated = true;
            return;
        }

        PushSplitRanges(range.Minimum, range.Maximum);
    }

    private void PushSplitRanges(int minimum, int maximum)
    {
        int midpoint = minimum + ((maximum - minimum) / 2);
        pendingRanges.Push(new SteamLobbyListQueryRange(midpoint + 1, maximum));
        pendingRanges.Push(new SteamLobbyListQueryRange(minimum, midpoint));
    }
}
