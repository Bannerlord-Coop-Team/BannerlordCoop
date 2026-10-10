using Common.Voice;
using Xunit;

namespace Common.Tests.Voice;

public class VoiceJitterBufferTests
{
    [Fact]
    public void ReordersStartupAndRejectsDuplicatesAndLateFrames()
    {
        var buffer = new VoiceJitterBuffer();
        Assert.True(buffer.Add(2, new byte[] { 2 }, 0));
        Assert.True(buffer.Add(1, new byte[] { 1 }, 10));
        Assert.False(buffer.Add(2, new byte[] { 2 }, 20));
        Assert.False(buffer.TryRead(59, out _));
        Assert.True(buffer.TryRead(60, out var first));
        Assert.Equal(1, first[0]);
        Assert.False(buffer.Add(1, new byte[] { 1 }, 61));
        Assert.True(buffer.TryRead(80, out var second));
        Assert.Equal(2, second[0]);
    }

    [Fact]
    public void SilentStartDoesNotConcealAndLossConcealmentIsBounded()
    {
        var buffer = new VoiceJitterBuffer();
        Assert.False(buffer.TryRead(1000, out _));
        buffer.Add(1, new byte[] { 1 }, 1000);
        Assert.True(buffer.TryRead(1060, out _));
        for (int i = 1; i <= 3; i++)
        {
            Assert.True(buffer.TryRead(1060 + (i * 20), out var missing));
            Assert.Null(missing);
        }
        Assert.False(buffer.TryRead(1140, out _));
        Assert.True(buffer.Add(99, new byte[] { 99 }, 2000));
        Assert.True(buffer.TryRead(2060, out var resumed));
        Assert.Equal(99, resumed[0]);
    }

    [Fact]
    public void DropsStaleQueuesAndHandlesSequenceWrap()
    {
        var buffer = new VoiceJitterBuffer();
        buffer.Add(uint.MaxValue, new byte[] { 1 }, 0);
        buffer.Add(0, new byte[] { 2 }, 20);
        Assert.True(buffer.TryRead(60, out _));
        Assert.True(buffer.TryRead(80, out var wrapped));
        Assert.Equal(2, wrapped[0]);
        Assert.False(buffer.TryRead(1000, out _));
        buffer.Add(1, new byte[] { 1 }, 1010);
        buffer.Clear();
        Assert.False(buffer.TryRead(1100, out _));
    }
}
