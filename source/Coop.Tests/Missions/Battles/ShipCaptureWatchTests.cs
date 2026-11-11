#if DEBUG
using Missions.Battles;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class ShipCaptureWatchTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(29.9, false)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    public void IsOver_AfterTheWatchSeconds(double seconds, bool expected)
    {
        Assert.Equal(expected, ShipCaptureWatch.IsOver(seconds));
    }

    // A second capture restarts the running watch; disposing stops it, and disposing twice is harmless.
    [Fact]
    public void StartTwice_ThenDispose_StopsTheWatch()
    {
        var watch = new ShipCaptureWatch();
        Assert.False(watch.IsRunning);

        watch.Start(Guid.NewGuid());
        watch.NoteTick();
        watch.Start(Guid.NewGuid());
        Assert.True(watch.IsRunning);

        watch.Dispose();
        watch.Dispose();
        Assert.False(watch.IsRunning);
    }
}
#endif
