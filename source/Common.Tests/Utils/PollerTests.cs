using Common.Util;

namespace Common.Tests.Utils;

public class PollerTests
{
    [Fact]
    public void IntervalCorrectness()
    {
        // Arrange
        const int expectedCount = 10;
        const int interval = 100;

        int actualCount = 0;
        var poller = new Poller((dt) => { actualCount++; }, TimeSpan.FromMilliseconds(interval));
        
        // Act
        poller.Start();
        Thread.Sleep(expectedCount * interval);
        poller.Stop();

        Thread.Sleep(expectedCount * interval);

        // Assert
        Assert.Equal(expectedCount, actualCount);
    }
}
