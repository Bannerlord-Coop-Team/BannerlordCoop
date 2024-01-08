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

        while (actualCount < expectedCount) { }

        var endTime = DateTime.Now;
        poller.Stop();

        var actualTimeMs = endTime - startTime;


        // Assert
        var expectedTimeMs = TimeSpan.FromMilliseconds(expectedCount * interval);

        // +/- 10% tolerance
        var tolerance = TimeSpan.FromMilliseconds(interval * expectedCount / 10);
        var lowRange = expectedTimeMs - tolerance;
        var highRange = expectedTimeMs + tolerance;

        Assert.InRange(actualTimeMs, lowRange, highRange);
    }
}
