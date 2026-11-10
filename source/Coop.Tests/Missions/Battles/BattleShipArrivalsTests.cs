using Missions.Battles;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 L6: hull and capture traffic applies from the naval tick, in arrival order.</summary>
public class BattleShipArrivalsTests
{
    // Arrivals apply in the order they came, and nothing before the tick applies them.
    [Fact]
    public void ApplyAll_RunsTheArrivalsInArrivalOrder()
    {
        var arrivals = new BattleShipArrivals();
        var applied = new List<string>();

        arrivals.Enqueue(() => applied.Add("confirm"), "confirm");
        arrivals.Enqueue(() => applied.Add("removal"), "removal");
        Assert.Empty(applied);
        Assert.Equal(2, arrivals.Count);

        arrivals.ApplyAll();

        Assert.Equal(new[] { "confirm", "removal" }, applied);
        Assert.Equal(0, arrivals.Count);
    }

    // One that throws is logged and skipped; one queued while applying waits for the next tick, so a pass always ends.
    [Fact]
    public void ApplyAll_SkipsAThrowingArrival_AndLeavesOnesQueuedMeanwhileForTheNextTick()
    {
        var arrivals = new BattleShipArrivals();
        var applied = new List<string>();
        arrivals.Enqueue(() => throw new InvalidOperationException(), "broken");
        arrivals.Enqueue(() =>
        {
            applied.Add("first");
            arrivals.Enqueue(() => applied.Add("later"), "later");
        }, "first");

        arrivals.ApplyAll();
        Assert.Equal(new[] { "first" }, applied);
        Assert.Equal(1, arrivals.Count);

        arrivals.ApplyAll();
        Assert.Equal(new[] { "first", "later" }, applied);
    }

    // Nothing queued applies after the battle ended.
    [Fact]
    public void Clear_DropsWhatWaits()
    {
        var arrivals = new BattleShipArrivals();
        bool applied = false;
        arrivals.Enqueue(() => applied = true, "late");

        arrivals.Clear();
        arrivals.ApplyAll();

        Assert.False(applied);
    }
}
