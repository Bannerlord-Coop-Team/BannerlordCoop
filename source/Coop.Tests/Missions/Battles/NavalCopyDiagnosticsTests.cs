#if COOP_NAVAL
using Missions.Naval;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyDiagnosticsTests
{
    [Fact]
    public void RollingWindow_KeepsOnlyTheLastTenSeconds()
    {
        var window = new RollingWindow();
        window.Add(0f, 100f);
        window.Add(5f, 1f);
        window.Add(12f, 3f);

        Assert.Equal(2, window.Count);
        Assert.Equal(2f, window.Mean, 4);
        Assert.Equal(3f, window.Max);
    }

    // R43: one 3.4 ms spike stood for the whole window's steer cost; the p99 of 100 frames skips the single worst one.
    [Fact]
    public void RollingWindow_Percentile_IsTheNearestRankValue()
    {
        var window = new RollingWindow();
        for (int frame = 1; frame <= 99; frame++) window.Add(frame * 0.01f, 0.03f);
        window.Add(1f, 3.4f);

        Assert.Equal(0.03f, window.Percentile(99f));
        Assert.Equal(3.4f, window.Percentile(100f));
        Assert.Equal(3.4f, window.Max);
        Assert.Equal(0f, new RollingWindow().Percentile(99f));
    }

    // The probe sums per-frame GC collection deltas over the window.
    [Fact]
    public void RollingWindow_Sum_AddsTheWindowsValues()
    {
        var window = new RollingWindow();
        window.Add(0f, 1f);
        window.Add(1f, 0f);
        window.Add(2f, 2f);

        Assert.Equal(3f, window.Sum);
    }

    [Fact]
    public void RollingWindow_IgnoresNonFiniteValues()
    {
        var window = new RollingWindow();
        window.Add(0f, float.NaN);
        window.Add(0f, float.PositiveInfinity);

        Assert.Equal(0, window.Count);
        Assert.Equal(0f, window.Mean);
        Assert.Equal(0f, window.Max);
    }

    [Fact]
    public void RollingWindow_CountsSignChangesOutsideTheEpsilon()
    {
        var window = new RollingWindow();
        float[] values = { 0.5f, -0.5f, 0.001f, -0.4f, 0.3f, 0.3f };
        for (int index = 0; index < values.Length; index++)
            window.Add(index * 0.1f, values[index]);

        Assert.Equal(2, window.SignChanges(0.01f));
    }

    [Fact]
    public void MeasureApproach_SideBySideHullsAreTheirSpacingApart()
    {
        // Two 30 m by 8 m hulls heading north, 20 m apart centre to centre.
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f,
            new Vec2(20f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(20f, measured.CenterDistance, 3);
        Assert.Equal(12f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_ABowAtTheBeamIsTheBowDistance()
    {
        // The rammer heads east, its bow 6 m from the target's keel; the target lies north-south.
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(-21f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        // 6 m from the keel is 2 m from the target's side.
        Assert.Equal(2f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_CrossingKeelsOverlap()
    {
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(-8f, measured.Gap, 3);
    }

    [Fact]
    public void SegmentDistance_MeasuresEndToEnd()
    {
        Assert.Equal(5f, NavalCopyProbeLogic.SegmentDistance(new Vec2(0f, 0f), new Vec2(10f, 0f), new Vec2(15f, 0f),
            new Vec2(20f, 0f)), 3);
    }
}
#endif
