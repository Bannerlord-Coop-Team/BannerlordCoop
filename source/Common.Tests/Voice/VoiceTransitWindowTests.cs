using Common.Voice;
using Xunit;

namespace Common.Tests.Voice;

public class VoiceTransitWindowTests
{
    [Fact]
    public void RejectsDelayedSpeechWithoutRequiringSynchronizedClocks()
    {
        var window = new VoiceTransitWindow();
        Assert.True(window.Accept(5000, 1000));
        Assert.True(window.Accept(5020, 1030));
        Assert.False(window.Accept(5040, 1500));
        Assert.True(window.Accept(5500, 1500));
        Assert.False(window.Accept(0, 1500));
    }

    [Fact]
    public void RollingBaselineAccommodatesSlowClockDrift()
    {
        var window = new VoiceTransitWindow();
        for (int i = 0; i < 10000; i++)
            Assert.True(window.Accept(1000 + (i * 100), 2000 + (i * 101)));
    }
}
