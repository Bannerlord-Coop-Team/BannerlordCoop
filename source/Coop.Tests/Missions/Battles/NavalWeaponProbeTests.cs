#if COOP_NAVAL
using GameInterface.Utils;
using Missions.Naval;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks that paired measurement windows fail explicitly instead of silently reporting partial data as complete.</summary>
public class NavalWeaponProbeTests
{
    /// <summary>Creates a probe at shared second ten without a native mission.</summary>
    private NavalWeaponProbe Create(out Mock<IWaterClock> clock)
    {
        clock = new Mock<IWaterClock>();
        clock.SetupGet(value => value.IsRedirected).Returns(true);
        clock.SetupGet(value => value.WrittenTime).Returns(100f);
        return new NavalWeaponProbe(clock.Object);
    }

    /// <summary>Invalid and already-started intervals cannot produce pairable measurements.</summary>
    [Theory]
    [InlineData(10f, 60f)]
    [InlineData(float.NaN, 60f)]
    [InlineData(float.PositiveInfinity, 60f)]
    [InlineData(20f, float.NaN)]
    [InlineData(20f, float.PositiveInfinity)]
    [InlineData(20f, 0f)]
    [InlineData(20f, 301f)]
    public void Start_RejectsInvalidWindow(float at, float seconds)
    {
        var probe = Create(out _);
        Assert.NotNull(probe.Start(null, at, seconds));
        Assert.False(probe.IsRunning);
    }

    /// <summary>Only the chosen shared-clock window is sampled; its end is an explicit completed result.</summary>
    [Fact]
    public void Window_ExcludesLeadInAndEndsAtDeadline()
    {
        var probe = Create(out _);
        Assert.Null(probe.Start(null, 20f, 60f));
        Assert.False(probe.InWindow(19f));
        Assert.True(probe.InWindow(20f));
        Assert.True(probe.InWindow(79.9f));
        Assert.False(probe.InWindow(80f));
        Assert.Equal("elapsed", (string)JObject.FromObject(probe.Status())["reason"]!);
    }

    /// <summary>A clock jump during migration invalidates the window instead of pairing different time bases.</summary>
    [Theory]
    [InlineData(19f, "clock_reversed")]
    [InlineData(float.NaN, "invalid_clock")]
    public void Window_StopsOnBrokenClock(float time, string reason)
    {
        var probe = Create(out _);
        Assert.Null(probe.Start(null, 20f, 60f));
        Assert.True(probe.InWindow(20f));
        Assert.False(probe.InWindow(time));
        Assert.False(probe.InWindow(21f));
        Assert.Equal(reason, (string)JObject.FromObject(probe.Status())["reason"]!);
    }

    /// <summary>A lost shared clock cannot leave the probe apparently running.</summary>
    [Fact]
    public void Window_StopsWhenClockRedirectIsLost()
    {
        var probe = Create(out var clock);
        Assert.Null(probe.Start(null, 20f, 60f));
        clock.SetupGet(value => value.IsRedirected).Returns(false);
        Assert.False(probe.InWindow(20f));
        Assert.Equal("clock_lost", (string)JObject.FromObject(probe.Status())["reason"]!);
        Assert.NotNull(probe.Start(null, 30f, 60f));
    }

    /// <summary>A command cannot discard a running window; an explicit stop permits another window.</summary>
    [Fact]
    public void Start_RequiresExplicitStopBeforeRestart()
    {
        var probe = Create(out _);
        Assert.Null(probe.Start(null, 20f, 60f));
        Assert.NotNull(probe.Start(null, 30f, 60f));
        probe.Stop("stopped");
        Assert.Null(probe.Start(null, 30f, 60f));
        Assert.True(probe.InWindow(30f));
    }
}
#endif
