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

    [Fact]
    public void UseBlockingTimeout_AppliesToABlockingRun()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var timeout = GameThread.Instance.UseBlockingTimeout(TimeSpan.FromMilliseconds(200));
        bool ran = false;
        Exception? failure = null;

        // Nothing pumps the queue, so the run expires after the scope's timeout instead of the usual 30 seconds
        var caller = new Thread(() =>
        {
            try
            {
                GameThread.Run(() => ran = true, blocking: true);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }) { IsBackground = true };
        caller.Start();

        Assert.True(caller.Join(TimeSpan.FromSeconds(10)), "the blocking run did not use the scope's timeout");
        var timedOut = Assert.IsType<TimeoutException>(failure);
        Assert.Contains("will not run", timedOut.Message);
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.False(ran);
    }
}
