using Common;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Common.Tests;

/// <summary>Verifies a blocking run that timed out or was canceled before it started never runs later.</summary>
[Collection(nameof(GameThreadCollection))]
public class GameThreadBlockingCompletionTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;

    public void Dispose()
    {
        GameThread.Instrument = false;
        GameThread.Instance.RestoreGameThread(previousGameThreadId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TimedOutAction_DoesNotRunOnALaterPump(bool unlimitedDrain, bool instrument)
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        IDisposable budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        bool ran = false;

        Exception? failure = RunBlockingOffThread(() => ran = true);
        if (unlimitedDrain) budget.Dispose();
        GameThread.Instrument = instrument;
        GameThread.Instance.Update(TimeSpan.Zero);
        budget.Dispose();

        var timeout = Assert.IsType<TimeoutException>(failure);
        Assert.False(ran);
        Assert.Contains("will not run", timeout.Message);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void SuccessfulBlockingRun_DisposesItsWaitHandle()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), LongTimeout);
        var caller = new BlockingCaller(() => { });
        GameThread.QueuedAction queued = WaitUntilQueued(queue, 1)[0];

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Null(caller.Join());
        Assert.True(queued.Wait.SafeWaitHandle.IsClosed);
    }

    [Fact]
    public void TimedOutBlockingRun_DisposesItsWaitHandle()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        var caller = new BlockingCaller(() => { });
        GameThread.QueuedAction queued = WaitUntilQueued(queue, 1)[0];

        Assert.IsType<TimeoutException>(caller.Join());
        Assert.True(queued.Wait.SafeWaitHandle.IsClosed);

        // A later pump must not signal the disposed handle.
        GameThread.Instance.Update(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimedOutOrCanceledRun_ReleasesItsClosureWithoutAPump(bool cancel)
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(
            TimeSpan.FromSeconds(1), cancel ? LongTimeout : ShortTimeout);
        using var cancellation = new CancellationTokenSource();

        WeakReference payload = QueueActionCapturingPayload(cancellation.Token, out BlockingCaller caller);
        if (cancel)
        {
            WaitUntilQueued(queue, 1);
            cancellation.Cancel();
        }
        Exception? failure = caller.Join();

        Assert.IsType(cancel ? typeof(OperationCanceledException) : typeof(TimeoutException), failure);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(payload.IsAlive, "the queue still holds the closure of an action that will never run");
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void ExpiredAction_KeepsFifoOrderForTheRest()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        var order = new List<int>();

        GameThread.EnqueueSafe(() => order.Add(1));
        Assert.IsType<TimeoutException>(RunBlockingOffThread(() => order.Add(2)));
        GameThread.EnqueueSafe(() => order.Add(3));
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { 1, 3 }, order);
    }

    [Fact]
    public void TimeoutWhileTheActionRuns_ReturnsTheRealResult()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        using var pump = new GameThreadPump(queue);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int runs = 0;

        var caller = new BlockingCaller(() =>
        {
            entered.Set();
            release.Wait();
            Interlocked.Increment(ref runs);
        });
        try
        {
            Assert.True(entered.Wait(LongTimeout));
            // Past the 1 s timeout and inside the second wait.
            Thread.Sleep(1500);
        }
        finally
        {
            release.Set();
        }

        Assert.Null(caller.Join());
        Assert.Equal(1, runs);
        Assert.Empty(pump.Stop());
    }

    [Fact]
    public void StartedActionThatOutlastsTheSecondWait_TimesOutAndStillRunsOnce()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        using var pump = new GameThreadPump(queue);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int runs = 0;

        var caller = new BlockingCaller(() =>
        {
            entered.Set();
            release.Wait();
            Interlocked.Increment(ref runs);
        });
        Exception? failure;
        try
        {
            Assert.True(entered.Wait(LongTimeout));
            failure = caller.Join();
        }
        finally
        {
            release.Set();
        }

        var timeout = Assert.IsType<TimeoutException>(failure);
        Assert.Contains("will finish on the game thread", timeout.Message);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref runs) == 1, LongTimeout));
        // The late completion must not signal the handle the caller already disposed.
        Assert.Empty(pump.Stop());
    }

    [Fact]
    public void CanceledSessionWhileTheActionRuns_ThrowsWithoutWaiting()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), LongTimeout);
        using var pump = new GameThreadPump(queue);
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int runs = 0;

        var caller = new BlockingCaller(() =>
        {
            entered.Set();
            release.Wait();
            Interlocked.Increment(ref runs);
        }, cancellation.Token);
        Exception? failure;
        try
        {
            Assert.True(entered.Wait(LongTimeout));
            cancellation.Cancel();
            failure = caller.Join(TimeSpan.FromSeconds(2));
        }
        finally
        {
            release.Set();
        }

        Assert.IsType<OperationCanceledException>(failure);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref runs) == 1, LongTimeout));
        Assert.Empty(pump.Stop());
    }

    [Fact]
    public void BlockingRun_PropagatesTheActionException_AndRunSafeDoesNot()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), LongTimeout);
        using var pump = new GameThreadPump(queue);

        Exception? plain = RunBlockingOffThread(() => throw new InvalidTimeZoneException("plain"));
        Exception? safe = new BlockingCaller(() => throw new InvalidTimeZoneException("safe"), safe: true).Join();

        Assert.IsType<InvalidTimeZoneException>(plain);
        Assert.Null(safe);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscardingTheQueue_ReleasesEveryBlockedCallerAndClosure(bool close)
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), LongTimeout);
        BlockingCaller[] callers = Enumerable.Range(0, 3).Select(_ => new BlockingCaller(() => { })).ToArray();
        GameThread.QueuedAction[] queued = WaitUntilQueued(queue, 3);

        int discarded = close
            ? GameThread.Instance.CloseAndDiscardQueuedActions(queue)
            : GameThread.Instance.DiscardQueuedActions(queue);

        Assert.Equal(3, discarded);
        Assert.All(callers, caller => Assert.IsType<OperationCanceledException>(caller.Join()));
        Assert.All(queued, item =>
        {
            Assert.Null(item.Act);
            Assert.True(item.Wait.SafeWaitHandle.IsClosed);
        });
    }

    [Fact]
    public void TimeoutRacingTheDequeue_HasOneWinnerPerCall()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(15));
        var random = new Random(3112);
        var calls = new List<(int[] Runs, BlockingCaller Caller)>();

        using (var pump = new GameThreadPump(queue, () => random.Next(0, 30)))
        {
            for (int index = 0; index < 300; index++)
            {
                var runs = new int[1];
                calls.Add((runs, new BlockingCaller(() => Interlocked.Increment(ref runs[0]))));
                if (index % 20 == 19) calls.ForEach(call => call.Caller.Join());
            }
            calls.ForEach(call => call.Caller.Join());
            Assert.True(SpinWait.SpinUntil(() => queue.Count == 0, LongTimeout));
            Assert.Empty(pump.Stop());
        }

        var outcomes = calls.Select(call => (Failure: call.Caller.Join(), Runs: Volatile.Read(ref call.Runs[0]))).ToList();
        Assert.All(outcomes, outcome => Assert.True(
            outcome.Failure switch
            {
                null => outcome.Runs == 1,
                TimeoutException timeout when timeout.Message.Contains("will not run") => outcome.Runs == 0,
                // A preempted pump can outlast the second wait; the action then finishes once.
                TimeoutException timeout when timeout.Message.Contains("started but did not finish") => outcome.Runs == 1,
                _ => false,
            },
            $"{outcome.Failure?.Message ?? "success"} with runs={outcome.Runs}"));
        Assert.Contains(outcomes, outcome => outcome.Failure == null);
        Assert.Contains(outcomes, outcome => outcome.Failure is TimeoutException);
    }

    private static Exception? RunBlockingOffThread(Action action) => new BlockingCaller(action).Join();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference QueueActionCapturingPayload(CancellationToken cancellation, out BlockingCaller caller)
    {
        var payload = new byte[1024];
        caller = new BlockingCaller(() => GC.KeepAlive(payload), cancellation);
        return new WeakReference(payload);
    }

    private static GameThread.QueuedAction[] WaitUntilQueued(GameThread.QueueContext queue, int count)
    {
        Assert.True(SpinWait.SpinUntil(() => queue.Count >= count, LongTimeout));
        lock (queue.gate) return queue.queue.ToArray();
    }

    /// <summary>
    /// A blocking caller on its own thread. Task.Run is avoided because a waiting test thread, which is the
    /// marked game thread, may run the task inline.
    /// </summary>
    private sealed class BlockingCaller
    {
        private readonly Thread thread;
        private Action? call;
        private Exception? failure;

        public BlockingCaller(Action action, CancellationToken cancellation = default, bool safe = false)
        {
            call = () =>
            {
                using (GameThread.ActivateCancellation(cancellation))
                {
                    if (safe) GameThread.RunSafe(action, blocking: true);
                    else GameThread.Run(action, blocking: true);
                }
            };
            thread = new Thread(Execute) { IsBackground = true };
            thread.Start();
        }

        public Exception? Join(TimeSpan? timeout = null)
        {
            Assert.True(thread.Join(timeout ?? LongTimeout), "the blocking caller did not return");
            return failure;
        }

        private void Execute()
        {
            // Drop the reference first so only the queue can keep the action alive.
            Action run = call!;
            call = null;
            try
            {
                run();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }
    }

    /// <summary>A game-loop thread that pumps one isolated queue until stopped.</summary>
    private sealed class GameThreadPump : IDisposable
    {
        private readonly Thread thread;
        private readonly ConcurrentQueue<Exception> faults = new();
        private volatile bool stopping;

        public GameThreadPump(GameThread.QueueContext queue, Func<int>? sleepMilliseconds = null)
        {
            using var ready = new ManualResetEventSlim();
            thread = new Thread(() =>
            {
                using (GameThread.ActivateQueue(queue))
                {
                    GameThread.Instance.MarkGameThread();
                    ready.Set();
                    while (!stopping)
                    {
                        try
                        {
                            GameThread.Instance.Update(TimeSpan.Zero);
                        }
                        catch (Exception exception)
                        {
                            faults.Enqueue(exception);
                        }
                        Thread.Sleep(sleepMilliseconds?.Invoke() ?? 1);
                    }
                }
            }) { IsBackground = true };
            thread.Start();
            ready.Wait();
        }

        public Exception[] Stop()
        {
            stopping = true;
            thread.Join();
            return faults.ToArray();
        }

        public void Dispose() => Stop();
    }
}

/// <summary>Serializes tests that mark the game thread or toggle the process-wide instrumentation.</summary>
[CollectionDefinition(nameof(GameThreadCollection), DisableParallelization = true)]
public sealed class GameThreadCollection { }
