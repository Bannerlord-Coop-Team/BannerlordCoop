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

        plan.AddResults(initialRange, new[] { 1UL, 2UL });

        Assert.False(plan.TryGetNext(out _));
        Assert.Equal(new[] { 1UL, 2UL }, plan.Results);
        Assert.False(plan.WasTruncated);
    }

    [Fact]
    public void SaturatedInitialQuery_SplitsEntireDiscoveryPartitionRange()
    {
        var plan = new SteamLobbyListQueryPlan();
        Assert.True(plan.TryGetNext(out var initialRange));

        plan.AddResults(initialRange, LobbyIds(1));

        Assert.True(plan.TryGetNext(out var lowerRange));
        Assert.False(lowerRange.IsUnfiltered);
        Assert.Equal(1, lowerRange.Minimum);
        Assert.Equal(1073741824, lowerRange.Maximum);
        Assert.False(lowerRange.Contains(0));
        Assert.True(lowerRange.Contains(1));
        Assert.True(lowerRange.Contains(1073741824));
        Assert.False(lowerRange.Contains(1073741825));

        Assert.True(plan.TryGetNext(out var upperRange));
        Assert.Equal(1073741825, upperRange.Minimum);
        Assert.Equal(int.MaxValue, upperRange.Maximum);
    }

    [Fact]
    public void SaturatedPartition_SplitsAndDeduplicatesResults()
    {
        var plan = new SteamLobbyListQueryPlan();
        Assert.True(plan.TryGetNext(out var initialRange));
        plan.AddResults(initialRange, LobbyIds(1));

        Assert.True(plan.TryGetNext(out var lowerRange));
        Assert.Equal(1, lowerRange.Minimum);
        Assert.Equal(1073741824, lowerRange.Maximum);

        plan.AddResults(lowerRange, LobbyIds(26));

        Assert.True(plan.TryGetNext(out var lowerQuarterRange));
        Assert.Equal(1, lowerQuarterRange.Minimum);
        Assert.Equal(536870912, lowerQuarterRange.Maximum);
        plan.AddResults(lowerQuarterRange, LobbyIds(51, 25));

        Assert.True(plan.TryGetNext(out var upperQuarterRange));
        Assert.Equal(536870913, upperQuarterRange.Minimum);
        Assert.Equal(1073741824, upperQuarterRange.Maximum);
        plan.AddResults(upperQuarterRange, Array.Empty<ulong>());

        Assert.True(plan.TryGetNext(out var upperRange));
        plan.AddResults(upperRange, LobbyIds(76, 25));

        Assert.False(plan.TryGetNext(out _));
        Assert.Equal(100, plan.Results.Count);
        Assert.Equal(Enumerable.Range(1, 100).Select(value => (ulong)value), plan.Results);
        Assert.False(plan.WasTruncated);
    }

    [Fact]
    public void SaturatedSingleValuePartition_ReportsTruncationWithoutLooping()
    {
        var plan = new SteamLobbyListQueryPlan();
        Assert.True(plan.TryGetNext(out var initialRange));
        plan.AddResults(initialRange, Array.Empty<ulong>());

        plan.AddResults(new SteamLobbyListQueryRange(7, 7), LobbyIds(1));

        Assert.False(plan.TryGetNext(out _));
        Assert.True(plan.WasTruncated);
    }

    [Fact]
    public void PartitionedQueries_HydrateMoreThanSteamResultLimit()
    {
        var lobbies = Enumerable.Range(1, 137)
            .Select(value => new
            {
                LobbyId = (ulong)value,
                Partition = LobbyDataCodec.GetDiscoveryPartition((ulong)value),
            })
            .ToArray();
        var plan = new SteamLobbyListQueryPlan();
        int queryCount = 0;

        while (plan.TryGetNext(out var range))
        {
            queryCount++;
            var page = lobbies
                .Where(lobby => range.IsUnfiltered ||
                    (lobby.Partition >= range.Minimum && lobby.Partition <= range.Maximum))
                .Take(SteamLobbyListQueryPlan.MaxResultsPerQuery)
                .Select(lobby => lobby.LobbyId)
                .ToArray();

            plan.AddResults(range, page);
        }

        Assert.InRange(queryCount, 2, int.MaxValue);
        Assert.Equal(lobbies.Select(lobby => lobby.LobbyId).OrderBy(id => id),
            plan.Results.OrderBy(id => id));
        Assert.False(plan.WasTruncated);
    }

    [Fact]
    public void DiscoveryPartition_DistributesClusteredLobbyIdsAcrossRange()
    {
        var partitions = Enumerable.Range(0, 100)
            .Select(offset => LobbyDataCodec.GetDiscoveryPartition(109775240917155000UL + (ulong)offset))
            .ToArray();

        Assert.All(partitions, partition => Assert.InRange(partition, 1, int.MaxValue));
        Assert.Contains(partitions, partition => partition < int.MaxValue / 2);
        Assert.Contains(partitions, partition => partition >= int.MaxValue / 2);
        Assert.Equal(partitions.Length, partitions.Distinct().Count());
    }

    private static IReadOnlyList<ulong> LobbyIds(
        int start,
        int count = SteamLobbyListQueryPlan.MaxResultsPerQuery)
    {
        return Enumerable.Range(start, count).Select(value => (ulong)value).ToArray();
    }
}
