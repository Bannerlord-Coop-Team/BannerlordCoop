using Common;
using System.Collections.Concurrent;

namespace Common.Tests;

/// <summary>
/// Verifies <see cref="GameThread.RunCleanupSafe"/>: cleanup whose blocking call times out still runs once with its
/// continuation on a later pump in the caller's session, and a cancelled session still throws at once.
/// </summary>
[Collection(nameof(GameThreadCollection))]
public class GameThreadCleanupTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;
    private readonly GameThread.QueueContext queue = new();
    private readonly IDisposable queueScope;
    private readonly CancellationTokenSource session = new();
    private readonly ConcurrentQueue<string> order = new();

    public GameThreadCleanupTests()
    {
        queueScope = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
    }

    public void Dispose()
    {
        GameThread.Instance.DiscardQueuedActions(queue);
        queueScope.Dispose();
        GameThread.Instance.RestoreGameThread(previousGameThreadId);
        session.Dispose();
    }

    [Fact]
    public void CleanupThatTimesOutBeforeItStarts_RunsOnceWithItsContinuationOnTheNextPump()
    {
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        // Nothing pumps while the caller waits, so the blocking cleanup expires before it starts.
        Assert.Null(CleanUpOnWorker(() => order.Enqueue("cleanup"), () => order.Enqueue("then")).Join());
        Assert.Empty(order);

        Pump();
        Pump();

        Assert.Equal(new[] { "cleanup", "then" }, order);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void CleanupThatTimesOutWhileItRuns_RunsOnceAndItsContinuationAfterIt()
    {
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Caller? caller = null;
        bool callerReturnedDuringCleanup = false;
        caller = CleanUpOnWorker(
            () =>
            {
                // Holds the game thread until the caller gave up on both waits.
                callerReturnedDuringCleanup = caller!.TryJoin(LongTimeout);
                order.Enqueue("cleanup");
            },
            () => order.Enqueue("then"));
        WaitUntilQueued();

        Pump();
        Pump();

        Assert.True(callerReturnedDuringCleanup, "the caller did not time out while the cleanup ran");
        Assert.Null(caller.Join());
        Assert.Equal(new[] { "cleanup", "then" }, order);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void QueuedCleanupThatThrows_StillRunsItsContinuation()
    {
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        int cleanups = 0;

        Assert.Null(CleanUpOnWorker(
            () =>
            {
                Interlocked.Increment(ref cleanups);
                throw new InvalidOperationException("cleanup failed");
            },
            () => order.Enqueue("then")).Join());
        Pump();
        Pump();

        Assert.Equal(1, cleanups);
        Assert.Equal(new[] { "then" }, order);
    }

    [Fact]
    public void CleanupThatTimedOut_AfterItsSessionEnded_IsDropped()
    {
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        Assert.Null(CleanUpOnWorker(() => order.Enqueue("cleanup"), () => order.Enqueue("then")).Join());

        // Another teardown ended this session first, so that teardown owns the cleanup.
        session.Cancel();
        Pump();

        Assert.Empty(order);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void CleanupWhoseSessionEndsDuringTheWait_ThrowsAtOnceAndQueuesNothing()
    {
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), LongTimeout);
        Caller caller = CleanUpOnWorker(() => order.Enqueue("cleanup"), () => order.Enqueue("then"));
        WaitUntilQueued();

        // A network shutdown waits for the poll callback, so the caller must not wait out the timeout.
        session.Cancel();
        Assert.True(caller.TryJoin(TimeSpan.FromSeconds(5)), "the caller kept waiting after its session ended");
        Pump();

        Assert.IsType<OperationCanceledException>(caller.Join());
        Assert.Empty(order);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void CleanupOnTheGameThread_RunsInlineWithItsContinuation()
    {
        GameThread.RunCleanupSafe(() => order.Enqueue("cleanup"), () => order.Enqueue("then"));

        Assert.Equal(new[] { "cleanup", "then" }, order);
        Assert.Equal(0, queue.Count);
    }

    private Caller CleanUpOnWorker(Action cleanup, Action then) => new(() =>
    {
        using (GameThread.ActivateCancellation(session.Token))
        {
            GameThread.RunCleanupSafe(cleanup, then, "cleanup-test");
        }
    });

    private void WaitUntilQueued() =>
        Assert.True(SpinWait.SpinUntil(() => queue.Count > 0, LongTimeout), "the cleanup was never queued");

    private static void Pump() => GameThread.Instance.Update(TimeSpan.Zero);

    /// <summary>
    /// The caller on its own thread, as the network poller runs it. Task.Run is avoided because a waiting test
    /// thread, which is the marked game thread, may run the task inline.
    /// </summary>
    private sealed class Caller
    {
        private readonly Thread thread;
        private Exception? failure;

        public Caller(Action call)
        {
            thread = new Thread(() =>
            {
                try
                {
                    call();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.Start();
        }

        public bool TryJoin(TimeSpan timeout) => thread.Join(timeout);

        public Exception? Join()
        {
            Assert.True(thread.Join(LongTimeout), "the caller did not return");
            return failure;
        }
    }
}
