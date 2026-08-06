using Common.Util;
using GameInterface.Services.Issues.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic;

/// <summary>
/// Exercises the real <see cref="PendingRegistry{TValue}"/> primitive directly - the exact
/// <c>Set</c>/<c>TryGet</c>/<c>Clear</c>/<c>ClearAll</c>/<c>Snapshot</c>/
/// <c>RestoreAll</c> semantics <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>/
/// <see cref="Interfaces.LordsNeedsTutorQuestFlags"/> already hand-implement today, unchanged by this file
/// (see <see cref="PendingRegistry{TValue}"/>'s own type doc comment for why neither existing registry is
/// rewired to delegate to this primitive in this phase).
/// </summary>
public class PendingRegistryTests
{
    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

    [Fact]
    public void TryGet_FalseWhenNothingWasEverSet()
    {
        var registry = new PendingRegistry<int>();
        var hero = NewHero();

        Assert.False(registry.TryGet(hero, out _));
    }

    [Fact]
    public void Set_ThenTryGet_RoundTripsTheExactValue()
    {
        var registry = new PendingRegistry<int>();
        var hero = NewHero();

        registry.Set(hero, 42);

        Assert.True(registry.TryGet(hero, out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Set_OverwritesAPreviousValueForTheSameKey()
    {
        var registry = new PendingRegistry<string>();
        var hero = NewHero();

        registry.Set(hero, "first");
        registry.Set(hero, "second");

        Assert.True(registry.TryGet(hero, out var value));
        Assert.Equal("second", value);
    }

    [Fact]
    public void Set_IgnoresANullOwner()
    {
        var registry = new PendingRegistry<int>();

        registry.Set(null, 7);

        Assert.False(registry.TryGet(null, out _));
    }

    [Fact]
    public void Clear_RemovesOnlyThatOwnersEntry()
    {
        var registry = new PendingRegistry<int>();
        var heroA = NewHero();
        var heroB = NewHero();
        registry.Set(heroA, 1);
        registry.Set(heroB, 2);

        registry.Clear(heroA);

        Assert.False(registry.TryGet(heroA, out _));
        Assert.True(registry.TryGet(heroB, out var valueB));
        Assert.Equal(2, valueB);
    }

    [Fact]
    public void Clear_OnAnUnknownOwner_IsANoOp()
    {
        var registry = new PendingRegistry<int>();
        var hero = NewHero();
        registry.Set(hero, 1);

        registry.Clear(NewHero());

        Assert.True(registry.TryGet(hero, out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void ClearAll_WipesEveryEntry()
    {
        var registry = new PendingRegistry<int>();
        registry.Set(NewHero(), 1);
        registry.Set(NewHero(), 2);

        registry.ClearAll();

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Snapshot_RoundTripsThroughRestoreAllExactlyAsAPersistencePatchRelies()
    {
        var registry = new PendingRegistry<string>();
        var heroA = NewHero();
        var heroB = NewHero();
        registry.Set(heroA, "player-A");
        registry.Set(heroB, "player-B");

        var snapshot = registry.Snapshot().ToList();
        Assert.Equal(2, snapshot.Count);

        var freshRegistry = new PendingRegistry<string>();
        freshRegistry.RestoreAll(snapshot);

        Assert.True(freshRegistry.TryGet(heroA, out var valueA));
        Assert.Equal("player-A", valueA);
        Assert.True(freshRegistry.TryGet(heroB, out var valueB));
        Assert.Equal("player-B", valueB);
    }

    [Fact]
    public void RestoreAll_ClearsExistingEntriesFirst()
    {
        var registry = new PendingRegistry<int>();
        var staleHero = NewHero();
        registry.Set(staleHero, 999);

        var freshHero = NewHero();
        registry.RestoreAll(new[] { new System.Collections.Generic.KeyValuePair<Hero, int>(freshHero, 1) });

        Assert.False(registry.TryGet(staleHero, out _));
        Assert.True(registry.TryGet(freshHero, out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void RestoreAll_IgnoresEntriesWithANullKey()
    {
        var registry = new PendingRegistry<int>();

        registry.RestoreAll(new[] { new System.Collections.Generic.KeyValuePair<Hero, int>(null, 1) });

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void RestoreAll_WithNullEntries_ClearsWithoutThrowing()
    {
        var registry = new PendingRegistry<int>();
        registry.Set(NewHero(), 1);

        registry.RestoreAll(null);

        Assert.Empty(registry.Snapshot());
    }
}
