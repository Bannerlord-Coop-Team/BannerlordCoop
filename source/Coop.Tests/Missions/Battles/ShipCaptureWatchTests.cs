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

    // The timer is started, restarted by a second capture and stopped, with ticks noted from the game thread in between.
    [Fact]
    public void StartTwice_NoteTicks_ThenDispose_LeavesNoTimerRunning()
    {
        var watch = new ShipCaptureWatch();

        watch.Start(Guid.NewGuid());
        watch.NoteTick();
        watch.Start(Guid.NewGuid());
        watch.NoteTick();
        watch.Dispose();
        watch.Dispose();
    }
}
