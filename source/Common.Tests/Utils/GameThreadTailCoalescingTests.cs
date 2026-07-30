using Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Common.Tests.Utils;

/// <summary>
/// Covers contiguous queue-tail coalescing without changing FIFO barriers between batches.
/// </summary>
[Collection(nameof(GameThreadTailCoalescingCollection))]
public sealed class GameThreadTailCoalescingTests : IDisposable
{
    public GameThreadTailCoalescingTests()
    {
        GameThread.Instance.MarkGameThread();
    }

    public void Dispose()
    {
        GameThread.Instance.Update(TimeSpan.Zero);
        GameThread.Instance.UnmarkGameThread();
    }

    [Fact]
    public void TryCoalesceWithQueuedTail_JoinsTheAdjacentBatch()
    {
        var token = new object();
        var batch = new List<string> { "ai" };
        var applied = new List<string>();
        bool joined = false;

        RunOffGameThread(() =>
        {
            GameThread.EnqueueSafeCoalescible(
                () => applied.AddRange(batch),
                token);
            joined = GameThread.TryCoalesceWithQueuedTail(
                token,
                () => batch.Insert(0, "player"));
        });

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.True(joined);
        Assert.Equal(new[] { "player", "ai" }, applied);
    }

    [Fact]
    public void TryCoalesceWithQueuedTail_DoesNotCrossAnInterveningAction()
    {
        var firstToken = new object();
        var secondToken = new object();
        var firstBatch = new List<string> { "ai" };
        var secondBatch = new List<string> { "player" };
        var applied = new List<string>();
        bool joined = true;

        RunOffGameThread(() =>
        {
            GameThread.EnqueueSafeCoalescible(
                () => applied.AddRange(firstBatch),
                firstToken);
            GameThread.Run(() => applied.Add("barrier"));
            joined = GameThread.TryCoalesceWithQueuedTail(
                firstToken,
                () => firstBatch.Insert(0, "player"));
            GameThread.EnqueueSafeCoalescible(
                () => applied.AddRange(secondBatch),
                secondToken);
        });

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.False(joined);
        Assert.Equal(new[] { "ai", "barrier", "player" }, applied);
    }

    [Fact]
    public void TryCoalesceWithQueuedTail_DoesNotJoinAfterTheQueueSnapshot()
    {
        var token = new object();
        bool joinedWhileDraining = true;

        RunOffGameThread(() =>
        {
            GameThread.EnqueueSafeCoalescible(
                () => joinedWhileDraining =
                    GameThread.TryCoalesceWithQueuedTail(token, () => { }),
                token);
        });

        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.False(joinedWhileDraining);
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
}

/// <summary>Serializes tests that directly own and drain the global game-thread queue.</summary>
[CollectionDefinition(
    nameof(GameThreadTailCoalescingCollection),
    DisableParallelization = true)]
public sealed class GameThreadTailCoalescingCollection
{
}
