using Common.Util;
using GameInterface.Services.TroopRosters;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.TroopRosters;

/// <summary>
/// Unit tests for <see cref="TroopRosterSyncCoalescer"/>, the batch that collapses many authoritative
/// roster changes into one snapshot per roster per frame. The dirty-set logic does not require the
/// game to run, so rosters are created without their constructor purely as distinct references.
/// </summary>
public class TroopRosterSyncCoalescerTests
{
    private static TroopRoster NewRoster() => ObjectHelper.SkipConstructor<TroopRoster>();

    private static (TroopRosterSyncCoalescer coalescer, List<TroopRoster> flushed) Build()
    {
        var flushed = new List<TroopRoster>();
        var coalescer = new TroopRosterSyncCoalescer
        {
            Flush = rosters => flushed.AddRange(rosters)
        };
        return (coalescer, flushed);
    }

    [Fact]
    public void Update_WithNoChanges_DoesNotInvokeFlush()
    {
        var flushInvoked = false;
        var coalescer = new TroopRosterSyncCoalescer { Flush = _ => flushInvoked = true };

        coalescer.Update(TimeSpan.Zero);

        Assert.False(flushInvoked);
    }

    [Fact]
    public void MarkDirty_SameRosterManyTimes_FlushesItOnce()
    {
        var (coalescer, flushed) = Build();
        var roster = NewRoster();

        coalescer.MarkDirty(roster);
        coalescer.MarkDirty(roster);
        coalescer.MarkDirty(roster);
        coalescer.Update(TimeSpan.Zero);

        Assert.Single(flushed);
        Assert.Same(roster, flushed[0]);
    }

    [Fact]
    public void MarkDirty_DistinctRosters_FlushesEachOnce()
    {
        var (coalescer, flushed) = Build();
        var first = NewRoster();
        var second = NewRoster();

        coalescer.MarkDirty(first);
        coalescer.MarkDirty(second);
        coalescer.MarkDirty(first);
        coalescer.Update(TimeSpan.Zero);

        Assert.Equal(2, flushed.Count);
        Assert.Contains(first, flushed);
        Assert.Contains(second, flushed);
    }

    [Fact]
    public void Update_ClearsDirtySet_SoSecondFlushSendsNothing()
    {
        var (coalescer, flushed) = Build();

        coalescer.MarkDirty(NewRoster());
        coalescer.Update(TimeSpan.Zero);
        Assert.Single(flushed);

        flushed.Clear();
        coalescer.Update(TimeSpan.Zero);
        Assert.Empty(flushed);
    }

    [Fact]
    public void MarkDirty_Null_IsIgnored()
    {
        var (coalescer, flushed) = Build();

        coalescer.MarkDirty(null);
        coalescer.Update(TimeSpan.Zero);

        Assert.Empty(flushed);
    }

    [Fact]
    public void Update_WithoutFlushSet_RetainsChangesUntilSinkInstalled()
    {
        var coalescer = new TroopRosterSyncCoalescer();
        var roster = NewRoster();
        coalescer.MarkDirty(roster);

        // No sink yet: Update must not throw and must not drop the change.
        coalescer.Update(TimeSpan.Zero);

        // Once a sink is installed, the earlier change is delivered rather than silently lost.
        var flushed = new List<TroopRoster>();
        coalescer.Flush = rosters => flushed.AddRange(rosters);
        coalescer.Update(TimeSpan.Zero);

        Assert.Single(flushed);
        Assert.Same(roster, flushed[0]);
    }
}
