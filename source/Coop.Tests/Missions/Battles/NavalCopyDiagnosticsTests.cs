#if DEBUG
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
        var measured = NavalCopyBodyLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f,
            new Vec2(20f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(20f, measured.CenterDistance, 3);
        Assert.Equal(12f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_ABowAtTheBeamIsTheBowDistance()
    {
        // The rammer heads east, its bow 6 m from the target's keel; the target lies north-south.
        var measured = NavalCopyBodyLogic.MeasureApproach(new Vec2(-21f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        // 6 m from the keel is 2 m from the target's side.
        Assert.Equal(2f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_CrossingKeelsOverlap()
    {
        var measured = NavalCopyBodyLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(-8f, measured.Gap, 3);
    }

    [Fact]
    public void SegmentDistance_MeasuresEndToEnd()
    {
        Assert.Equal(5f, NavalCopyBodyLogic.SegmentDistance(new Vec2(0f, 0f), new Vec2(10f, 0f), new Vec2(15f, 0f),
            new Vec2(20f, 0f)), 3);
    }
}
#endif
