using Common;

namespace Common.Tests;

/// <summary>Verifies that <see cref="GameThread.FrameObserver"/> sees every drained frame, including one whose action throws.</summary>
[Collection(nameof(GameThreadCollection))]
public class GameThreadFrameObserverTests : IDisposable
{
    private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;
    private readonly GameThread.QueueContext queue = new();
    private readonly IDisposable queueScope;
    private readonly List<(long Started, long Finished)> frames = new();

    public GameThreadFrameObserverTests()
    {
        queueScope = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        GameThread.Instance.FrameObserver = new RecordingObserver(frames);
    }

    public void Dispose()
    {
        GameThread.Instance.FrameObserver = null;
        GameThread.Instance.DiscardQueuedActions(queue);
        queueScope.Dispose();
        GameThread.Instance.RestoreGameThread(previousGameThreadId);
    }

    [Fact]
    public void ObserverTimesEachDrain_EvenWhenAnActionThrows()
    {
        bool ran = false;
        GameThread.EnqueueSafe(() => ran = true);
        GameThread.Instance.Update(TimeSpan.Zero);

        // Run from another thread queues the action unwrapped, so its exception escapes Update.
        var queuing = new Thread(() => GameThread.Run(() => throw new InvalidOperationException("boom")));
        queuing.Start();
        queuing.Join();
        Assert.Throws<InvalidOperationException>(() => GameThread.Instance.Update(TimeSpan.Zero));

        Assert.True(ran);
        Assert.Equal(2, frames.Count);
        Assert.All(frames, frame => Assert.True(frame.Finished >= frame.Started));
    }

    private sealed class RecordingObserver : IGameThreadFrameObserver
    {
        private readonly List<(long, long)> frames;

        public RecordingObserver(List<(long, long)> frames)
        {
            this.frames = frames;
        }

        public void OnFrame(long drainStartedTimestamp, long drainFinishedTimestamp) =>
            frames.Add((drainStartedTimestamp, drainFinishedTimestamp));
    }
}
