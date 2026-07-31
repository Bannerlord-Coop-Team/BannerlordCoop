using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Common.Tests.Utils;

/// <summary>Covers adjacent game-thread batching without crossing FIFO barriers.</summary>
[Collection(nameof(GameThreadBatchedRunCollection))]
public sealed class GameThreadBatchedRunTests : IDisposable
{
    public GameThreadBatchedRunTests()
    {
        GameThread.Instance.MarkGameThread();
        GameThread.Instance.Update(TimeSpan.Zero);
    }

    public void Dispose()
    {
        GameThread.Instance.Update(TimeSpan.Zero);
        GameThread.Instance.UnmarkGameThread();
    }

    [Fact]
    public void RunSafeBatched_CollapsesAdjacentItemsIntoOneQueueSlot()
    {
        var applied = new List<int>();
        Action<int> apply = applied.Add;

        RunOffGameThread(() =>
        {
            for (int i = 0; i < 134; i++)
                GameThread.RunSafeBatched(i, apply);
        });

        Assert.Equal(1, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(Enumerable.Range(0, 134), applied);
    }

    [Fact]
    public void RunSafeBatched_DoesNotCrossAnInterveningAction()
    {
        var applied = new List<string>();
        Action<string> apply = applied.Add;

        RunOffGameThread(() =>
        {
            GameThread.RunSafeBatched("first", apply);
            GameThread.RunSafeBatched("second", apply);
            GameThread.Run(() => applied.Add("barrier"));
            GameThread.RunSafeBatched("third", apply);
        });

        Assert.Equal(3, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(
            new[] { "first", "second", "barrier", "third" },
            applied);
    }

    [Fact]
    public void RunSafeBatched_DoesNotJoinDifferentActions()
    {
        var applied = new List<string>();
        Action<int> first = item => applied.Add($"first:{item}");
        Action<int> second = item => applied.Add($"second:{item}");

        RunOffGameThread(() =>
        {
            GameThread.RunSafeBatched(1, first);
            GameThread.RunSafeBatched(2, second);
        });

        Assert.Equal(2, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { "first:1", "second:2" }, applied);
    }

    [Fact]
    public void RunSafeBatched_DoesNotAppendAfterTheQueueSnapshot()
    {
        var probe = new SnapshotProbe();

        RunOffGameThread(() =>
            GameThread.RunSafeBatched(1, probe.Apply));

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { 1 }, probe.Applied);
        Assert.Equal(1, GameThread.Instance.QueueLength);

        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1, 2 }, probe.Applied);
    }

    [Fact]
    public void RunSafeBatched_DoesNotJoinDifferentCancellationScopes()
    {
        var applied = new List<string>();
        Action<string> apply = applied.Add;
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();

        RunOffGameThread(() =>
        {
            using (GameThread.ActivateCancellation(firstCancellation.Token))
                GameThread.RunSafeBatched("first", apply);

            using (GameThread.ActivateCancellation(secondCancellation.Token))
                GameThread.RunSafeBatched("second", apply);
        });

        Assert.Equal(2, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { "first", "second" }, applied);
    }

    [Fact]
    public void RunSafeBatched_IgnoresAnAlreadyCanceledScope()
    {
        var applied = new List<int>();
        Action<int> apply = applied.Add;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        RunOffGameThread(() =>
        {
            using (GameThread.ActivateCancellation(cancellation.Token))
                GameThread.RunSafeBatched(1, apply);
        });

        Assert.Equal(0, GameThread.Instance.QueueLength);
        Assert.Empty(applied);
    }

    [Fact]
    public void RunSafeBatched_DiscardsABatchCanceledBeforeDrain()
    {
        var applied = new List<int>();
        Action<int> apply = applied.Add;
        using var cancellation = new CancellationTokenSource();

        RunOffGameThread(() =>
        {
            using (GameThread.ActivateCancellation(cancellation.Token))
                GameThread.RunSafeBatched(1, apply);
        });

        cancellation.Cancel();
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Empty(applied);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    [Fact]
    public void RunSafeBatched_DiscardsItemsWhenTheirLifetimeEnds()
    {
        var applied = new List<int>();
        Action<int> apply = applied.Add;
        var lifetime = new CancellationTokenSource();

        RunOffGameThread(() =>
        {
            for (int i = 0; i < 134; i++)
                GameThread.RunSafeBatched(i, apply, lifetime.Token);
        });

        Assert.Equal(1, GameThread.Instance.QueueLength);
        lifetime.Cancel();
        lifetime.Dispose();
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Empty(applied);
    }

    [Fact]
    public void RunSafeBatched_ContinuesAfterAnItemThrows()
    {
        var applied = new List<int>();
        Action<int> apply = item =>
        {
            if (item == 1)
                throw new InvalidOperationException("Expected test failure");
            applied.Add(item);
        };

        RunOffGameThread(() =>
        {
            for (int i = 0; i < 3; i++)
                GameThread.RunSafeBatched(i, apply);
        });

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { 0, 2 }, applied);
    }

    [Fact]
    public void RunSafeBatched_StopsWhenItsScopeIsCanceledMidBatch()
    {
        var applied = new List<int>();
        using var cancellation = new CancellationTokenSource();
        Action<int> apply = item =>
        {
            applied.Add(item);
            cancellation.Cancel();
        };

        RunOffGameThread(() =>
        {
            using (GameThread.ActivateCancellation(cancellation.Token))
            {
                GameThread.RunSafeBatched(1, apply);
                GameThread.RunSafeBatched(2, apply);
            }
        });

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { 1 }, applied);
    }

    private static void RunOffGameThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }

    /// <summary>Queues a second item while the first queue snapshot is draining.</summary>
    private sealed class SnapshotProbe
    {
        public SnapshotProbe()
        {
            Apply = ApplyItem;
        }

        public List<int> Applied { get; } = new List<int>();
        public Action<int> Apply { get; }

        private void ApplyItem(int item)
        {
            Applied.Add(item);
            if (item != 1) return;

            RunOffGameThread(() =>
                GameThread.RunSafeBatched(2, Apply));
        }
    }
}

/// <summary>Serializes tests that directly own and drain the global game-thread queue.</summary>
[CollectionDefinition(
    nameof(GameThreadBatchedRunCollection),
    DisableParallelization = true)]
public sealed class GameThreadBatchedRunCollection
{
}
