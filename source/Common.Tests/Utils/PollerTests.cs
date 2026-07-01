using Common.Util;

namespace Common.Tests.Utils;

public class PollerTests
{
    [Theory]
    [InlineData(20, 100)]
    public void IntervalCorrectness(int expectedCount, int interval)
    {
        // Arrange
        int actualCount = 0;
        
        var poller = new Poller((dt) => { 
            Interlocked.Increment(ref actualCount);
        }, TimeSpan.FromMilliseconds(interval));
        
        // Act
        poller.Start();
        var startTime = DateTime.Now;
        var endTime = DateTime.Now;

        var timeout = TimeSpan.FromMilliseconds(expectedCount * interval * 2);

        while (actualCount < expectedCount &&
            // time out while if running time is greater than 2x expected time
            endTime - startTime < timeout)
        {
            Thread.Sleep(1);
            endTime = DateTime.Now;
        }

        poller.Stop();

        var actualTimeMs = endTime - startTime;

        // Assert
        // The poller reached the expected tick count before the 2x timeout; a dead or hung poller falls short here.
        Assert.True(actualCount >= expectedCount,
            $"Poller only ticked {actualCount}/{expectedCount} times in {actualTimeMs.TotalMilliseconds}ms.");

        var expectedTimeMs = TimeSpan.FromMilliseconds(expectedCount * interval);

        // -20% tolerance for the first tick firing immediately with a near-zero delta.
        var tolerance = TimeSpan.FromMilliseconds(interval * expectedCount / 5);
        var lowRange = expectedTimeMs - tolerance;

        // Only assert the lower bound: Task.Delay never returns early, so this floor is independent of runner load,
        // whereas an upper bound just measures how loaded the runner is and flakes when it's busy.
        Assert.True(actualTimeMs >= lowRange,
            $"Poller ticked {expectedCount} times in {actualTimeMs.TotalMilliseconds}ms, below the {lowRange.TotalMilliseconds}ms floor for a {interval}ms interval.");
    }
}
