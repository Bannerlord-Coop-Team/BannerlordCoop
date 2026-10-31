#if COOP_NAVAL
using GameInterface.Utils;
using Missions.Battles;
using Missions.Naval;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks measurement windows and failure reasons without loading a live mission or physics body.</summary>
public class NavalRollProbeTests
{
    /// <summary>Creates a sampler at shared second 10; water units advance ten times as fast.</summary>
    private NavalRollProbe Create()
    {
        var clock = new Mock<IWaterClock>();
        clock.SetupGet(value => value.IsRedirected).Returns(true);
        clock.SetupGet(value => value.WrittenTime).Returns(100f);
        return new NavalRollProbe(clock.Object);
    }

    /// <summary>Arms a synthetic registry identity whose native hull is never read by these tests.</summary>
    private string Start(NavalRollProbe probe, float at = 20f, float seconds = 60f) =>
        probe.Start(new NetworkShipInfo(Guid.NewGuid(), "owner", "party", false, null, null), "battle", "copy", at, seconds);

    /// <summary>A late start cannot masquerade as a complete paired interval.</summary>
    [Theory]
    [InlineData(10f, 60f)]
    [InlineData(float.NaN, 60f)]
    [InlineData(float.PositiveInfinity, 60f)]
    [InlineData(20f, float.NaN)]
    [InlineData(20f, 0f)]
    [InlineData(20f, 301f)]
    public void Start_RejectsInvalidIntervals(float at, float seconds)
    {
        var probe = Create();
        Assert.NotNull(Start(probe, at, seconds));
        Assert.False(probe.IsRunning);
    }

    /// <summary>Only samples within the chosen interval contribute, with signed angles preserved.</summary>
    [Fact]
    public void Record_UsesWindowAndRetainsSignedRoll()
    {
        var probe = Create();
        Assert.Null(Start(probe));
        probe.Record(19f, 90f, 0f, 0f);
        probe.Record(20f, -2f, 1f, 300f);
        probe.Record(20.01f, 90f, 0f, 0f);
        probe.Record(21f, 1f, 3f, 302f);
        probe.Record(80f, 90f, 0f, 0f);
        var status = JObject.FromObject(probe.Status());
        Assert.False(probe.IsRunning);
        Assert.Equal("elapsed", (string)status["reason"]!);
        Assert.Equal(2, (int)status["samples"]!);
        Assert.Equal(-0.5f, (float)status["rollMeanDeg"]!);
        Assert.Equal(2f, (float)status["pitchMeanDeg"]!);
        Assert.Equal(301f, (float)status["heaveMeanM"]!);
    }

    /// <summary>A stop or backward clock makes the partial run explicit and prevents further samples.</summary>
    [Theory]
    [InlineData(true, "clock_reversed")]
    [InlineData(false, "stopped")]
    public void Record_StopsPartialIntervals(bool reverse, string reason)
    {
        var probe = Create();
        Assert.Null(Start(probe));
        probe.Record(21f, 1f, 0f, 0f);
        if (reverse) probe.Record(20f, 2f, 0f, 0f);
        else probe.Stop("stopped");
        probe.Record(22f, 3f, 0f, 0f);
        var status = JObject.FromObject(probe.Status());
        Assert.Equal(reason, (string)status["reason"]!);
        Assert.Equal(1, (int)status["samples"]!);
    }

    /// <summary>Invalid measurements invalidate the run instead of biasing its mean toward zero.</summary>
    [Theory]
    [InlineData(20f, float.NaN)]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    public void Record_RejectsNonFiniteMeasurements(float time, float roll)
    {
        var probe = Create();
        Assert.Null(Start(probe));
        probe.Record(time, roll, 0f, 0f);
        Assert.Equal("invalid_sample", (string)JObject.FromObject(probe.Status())["reason"]!);
        Assert.False(probe.IsRunning);
    }

    /// <summary>Repeated starts do not silently replace data; an explicit stop permits a fresh run.</summary>
    [Fact]
    public void Start_RequiresStopBeforeRestart()
    {
        var probe = Create();
        Assert.Null(Start(probe));
        probe.Record(20f, 1f, 0f, 0f);
        Assert.NotNull(Start(probe));
        Assert.Equal(1, (int)JObject.FromObject(probe.Status())["samples"]!);
        probe.Stop("stopped");
        Assert.Null(Start(probe));
        Assert.Equal(0, (int)JObject.FromObject(probe.Status())["samples"]!);
    }

    /// <summary>A lost redirect stops the run before any native hull access.</summary>
    [Fact]
    public void Tick_StopsWhenSharedClockIsLost()
    {
        var clock = new Mock<IWaterClock>();
        clock.SetupGet(value => value.IsRedirected).Returns(true);
        var probe = new NavalRollProbe(clock.Object);
        Assert.Null(Start(probe));
        clock.SetupGet(value => value.IsRedirected).Returns(false);
        probe.Tick();
        Assert.Equal("clock_lost", (string)JObject.FromObject(probe.Status())["reason"]!);
    }

    /// <summary>An authority change cannot mix owner and copy roles inside one measurement.</summary>
    [Fact]
    public void Tick_StopsWhenAuthorityChanges()
    {
        var clock = new Mock<IWaterClock>();
        clock.SetupGet(value => value.IsRedirected).Returns(true);
        var probe = new NavalRollProbe(clock.Object);
        var ship = new NetworkShipInfo(Guid.NewGuid(), "owner", "party", false, null, null);
        Assert.Null(probe.Start(ship, "battle", "copy", 20f, 60f));
        ship.CurrentAuthority = "copy";
        probe.Tick();
        Assert.Equal("authority_or_hull_changed", (string)JObject.FromObject(probe.Status())["reason"]!);
    }

    /// <summary>Unsynchronized clocks cannot produce joinable samples.</summary>
    [Fact]
    public void Start_RequiresSharedClock()
    {
        var probe = new NavalRollProbe(Mock.Of<IWaterClock>());
        Assert.NotNull(Start(probe));
        Assert.False(probe.IsRunning);
    }
}
#endif
