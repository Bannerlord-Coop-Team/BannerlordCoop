using Coop.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Steam;

public class SteamLobbyListQueryPlanTests
{
    [Fact]
    public void UnsaturatedInitialQuery_CompletesWithoutPartitioning()
    {
        var plan = new SteamLobbyListQueryPlan();

        Assert.True(plan.TryGetNext(out var initialRange));
        Assert.True(initialRange.IsUnfiltered);

        plan.AddResults(initialRange, LobbyResults(1, 2));

        Assert.False(plan.TryGetNext(out _));
        Assert.Equal(new[] { 1UL, 2UL }, plan.Results);
        Assert.False(plan.WasTruncated);
    }

    [Fact]
    public void SaturatedInitialQuery_SplitsAtReturnedServerSteamIdMedian()
    {
        var plan = new SteamLobbyListQueryPlan();
        Assert.True(plan.TryGetNext(out var initialRange));

        plan.AddResults(initialRange, LobbyResults(1));

        ulong lowerMaximum = SteamLobbyListQueryRange.MinimumServerSteamId + 24;
        Assert.True(plan.TryGetNext(out var lowerRange));
        Assert.False(lowerRange.IsUnfiltered);
        Assert.Equal(SteamLobbyListQueryRange.MinimumServerSteamId, lowerRange.Minimum);
        Assert.Equal(lowerMaximum, lowerRange.Maximum);

        Assert.True(plan.TryGetNext(out var upperRange));
        Assert.Equal(lowerMaximum + 1, upperRange.Minimum);
        Assert.Equal(SteamLobbyListQueryRange.MaximumServerSteamId, upperRange.Maximum);
    }

    [Fact]
    public void SaturatedSingleServerSteamId_ReportsTruncationWithoutLooping()
    {
        var plan = new SteamLobbyListQueryPlan();
        Assert.True(plan.TryGetNext(out var initialRange));
        plan.AddResults(initialRange, Array.Empty<SteamLobbyListQueryResult>());

        ulong serverSteamId = SteamLobbyListQueryRange.MinimumServerSteamId + 7;
        var range = new SteamLobbyListQueryRange(serverSteamId, serverSteamId);
        var lobbies = Enumerable.Range(1, SteamLobbyListQueryPlan.MaxResultsPerQuery)
            .Select(lobbyId => new SteamLobbyListQueryResult((ulong)lobbyId, serverSteamId))
            .ToArray();
        plan.AddResults(range, lobbies);

        Assert.False(plan.TryGetNext(out _));
        Assert.True(plan.WasTruncated);
    }

    [Fact]
    public void ServerSteamIdQueries_HydrateMoreThanSteamResultLimit()
    {
        var lobbies = LobbyResults(1, 137).ToArray();
        var plan = new SteamLobbyListQueryPlan();
        int queryCount = 0;

        while (plan.TryGetNext(out var range))
        {
            queryCount++;
            var page = lobbies
                .Where(lobby => range.Contains(lobby.ServerSteamId))
                .Take(SteamLobbyListQueryPlan.MaxResultsPerQuery)
                .ToArray();

            plan.AddResults(range, page);
        }

        Assert.InRange(queryCount, 2, int.MaxValue);
        Assert.Equal(lobbies.Select(lobby => lobby.LobbyId).OrderBy(id => id),
            plan.Results.OrderBy(id => id));
        Assert.False(plan.WasTruncated);
    }

    [Fact]
    public void ServerSteamIdRange_CoversGameServerAndAnonymousGameServerAccounts()
    {
        Assert.True(SteamLobbyListQueryRange.IsServerSteamId(85568392920039424UL));
        Assert.True(SteamLobbyListQueryRange.IsServerSteamId(90071992547409919UL));
        Assert.True(SteamLobbyListQueryRange.IsServerSteamId(90071992547409920UL));
        Assert.True(SteamLobbyListQueryRange.IsServerSteamId(94575592174780415UL));
        Assert.False(SteamLobbyListQueryRange.IsServerSteamId(85568392920039423UL));
        Assert.False(SteamLobbyListQueryRange.IsServerSteamId(94575592174780416UL));
    }

    private static IReadOnlyList<SteamLobbyListQueryResult> LobbyResults(
        int start,
        int count = SteamLobbyListQueryPlan.MaxResultsPerQuery)
    {
        return Enumerable.Range(start, count)
            .Select(value => new SteamLobbyListQueryResult(
                (ulong)value,
                SteamLobbyListQueryRange.MinimumServerSteamId + (ulong)value - 1))
            .ToArray();
    }
}
