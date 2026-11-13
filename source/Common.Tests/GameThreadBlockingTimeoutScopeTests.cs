using Common;

namespace Common.Tests;

/// <summary>Verifies a blocking timeout scope changes only the wait, never the frame drain.</summary>
[Collection(nameof(GameThreadCollection))]
public class GameThreadBlockingTimeoutScopeTests : IDisposable
{
    private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;

    public void Dispose()
    {
        GameThread.Instance.RestoreGameThread(previousGameThreadId);
    }

    [Fact]
    public void UseBlockingTimeout_KeepsAnActiveDrainLimit()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var drainLimit = GameThread.Instance.LimitFrameDrain(TimeSpan.FromMilliseconds(8));
        long limitedDrain = Interlocked.Read(ref queue.activeDrainTicks);
        TimeSpan longer = TimeSpan.FromMinutes(5);

        using (GameThread.Instance.UseBlockingTimeout(longer))
        {
            Assert.Equal(longer, GameThread.Instance.EffectiveBlockingTimeout);
            Assert.Equal(1, GameThread.Instance.FrameDrainLimitCount);
            Assert.Equal(limitedDrain, Interlocked.Read(ref queue.activeDrainTicks));
        }

        Assert.Equal(GameThread.BlockingTimeout, GameThread.Instance.EffectiveBlockingTimeout);
        Assert.Equal(limitedDrain, Interlocked.Read(ref queue.activeDrainTicks));
    }
}
