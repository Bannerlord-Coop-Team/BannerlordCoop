using Missions.Agents.Packets;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Agents;

// #4486: the per-frame action poll must not reach the reflective native name lookup for a code it already saw.
public class ActionNameCacheTests
{
    private readonly Dictionary<int, string?> names = new Dictionary<int, string?>();
    private readonly Dictionary<string, int> indexes = new Dictionary<string, int>();
    private int nameReads;
    private int indexReads;

    private ActionNameCache CreateCache() => new ActionNameCache(
        code =>
        {
            nameReads++;
            return names.TryGetValue(code, out string? name) ? name : null;
        },
        name =>
        {
            indexReads++;
            return indexes.TryGetValue(name, out int index) ? index : -1;
        });

    [Fact]
    public void GetName_ReadsTheSourceOnceThenServesTheCachedName()
    {
        names[12] = "act_walk_forward";
        var cache = CreateCache();

        Assert.Equal("act_walk_forward", cache.GetName(12));
        Assert.Equal("act_walk_forward", cache.GetName(12));
        Assert.Equal("act_walk_forward", cache.GetName(12));

        Assert.Equal(1, nameReads);
    }

    [Fact]
    public void GetName_NoAnimationResolver_IsNotCachedSoTheFallbackKeepsWorking()
    {
        var cache = CreateCache();

        Assert.Null(cache.GetName(7));
        Assert.Null(cache.GetName(7));
        Assert.Equal(2, nameReads);
        Assert.Equal(0, cache.Count);

        names[7] = "act_idle";
        Assert.Equal("act_idle", cache.GetName(7));
        Assert.Equal("act_idle", cache.GetName(7));
        Assert.Equal(3, nameReads);
    }

    [Theory]
    [InlineData("act_usage_ballista_idle_defender", true, false)]
    [InlineData("act_usage_ballista_ammo_pick_up_start_defender", true, false)]
    [InlineData("act_usage_mangonel_big_shoot", true, false)]
    [InlineData("act_usage_mangonel_reload_2_idle", true, false)]
    [InlineData("act_usage_trebuchet_idle", false, false)]
    [InlineData("act_command", false, true)]
    [InlineData("act_command_follow", false, true)]
    [InlineData("act_horse_command", false, true)]
    [InlineData("act_horse_command_charge", false, true)]
    [InlineData("act_walk_forward", false, false)]
    [InlineData("act_mount", false, false)]
    public void Describe_ClassifiesTheNameLikeTheUncachedChecks(string name, bool siegeUsage, bool orderGesture)
    {
        var cache = CreateCache();

        ActionNameCache.Entry entry = cache.Describe(40, name);

        Assert.Equal(siegeUsage, entry.IsRangedSiegeUsage);
        Assert.Equal(orderGesture, entry.IsOrderGesture);
    }

    [Fact]
    public void Describe_SameNameInstance_ReusesTheEntryWithoutReadingTheSourceAgain()
    {
        names[9] = "act_usage_ballista_idle_defender";
        var cache = CreateCache();

        ActionNameCache.Entry first = cache.Describe(9, cache.GetName(9));
        ActionNameCache.Entry second = cache.Describe(9, cache.GetName(9));

        Assert.Same(first, second);
        Assert.True(first.IsRangedSiegeUsage);
        Assert.Equal(1, nameReads);
    }

    [Fact]
    public void GetName_AfterDescribeWithAPatchedName_StillAsksTheSource()
    {
        // A patched name source hands Describe a name the native tables never gave.
        var cache = CreateCache();
        cache.Describe(6, "act_usage_mangonel_big_idle");

        Assert.Null(cache.GetName(6));
        Assert.Equal(1, nameReads);
    }

    [Fact]
    public void Describe_ChangedName_ReplacesTheEntryInsteadOfServingTheStaleClassification()
    {
        var cache = CreateCache();
        cache.Describe(5, "act_walk_forward");

        // A different instance is the only sign the name source changed, e.g. a patched resolver in a test run.
        ActionNameCache.Entry replaced = cache.Describe(5, new string("act_usage_mangonel_big_idle".ToCharArray()));

        Assert.True(replaced.IsRangedSiegeUsage);
        Assert.Same(replaced, cache.Describe(5, replaced.Name));
    }

    [Fact]
    public void ResolveIndex_AsksTheIndexSourceOnceForAValidIndex()
    {
        indexes["act_walk_forward"] = 321;
        var cache = CreateCache();

        Assert.Equal(321, cache.ResolveIndex(321, "act_walk_forward"));
        Assert.Equal(321, cache.ResolveIndex(321, "act_walk_forward"));

        Assert.Equal(1, indexReads);
    }

    [Fact]
    public void ResolveIndex_UnknownName_IsReportedEveryTimeAndNeverCached()
    {
        var cache = CreateCache();

        Assert.Equal(-1, cache.ResolveIndex(8, "act_not_loaded_yet"));
        Assert.Equal(-1, cache.ResolveIndex(8, "act_not_loaded_yet"));
        Assert.Equal(2, indexReads);

        indexes["act_not_loaded_yet"] = 8;
        Assert.Equal(8, cache.ResolveIndex(8, "act_not_loaded_yet"));
        Assert.Equal(8, cache.ResolveIndex(8, "act_not_loaded_yet"));
        Assert.Equal(3, indexReads);
    }

    [Fact]
    public void ResolveIndex_ChangedName_ResolvesAgain()
    {
        indexes["act_a"] = 1;
        indexes["act_b"] = 2;
        var cache = CreateCache();

        Assert.Equal(1, cache.ResolveIndex(4, "act_a"));
        Assert.Equal(2, cache.ResolveIndex(4, new string("act_b".ToCharArray())));

        Assert.Equal(2, indexReads);
    }
}
