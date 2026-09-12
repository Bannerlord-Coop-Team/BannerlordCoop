using Xunit;
using Common;

namespace Common.Tests;

/// <summary>Verifies frame budgets preserve queue order, cancellation and runtime ownership.</summary>
public class GameThreadDrainBudgetTests : IDisposable
{
    private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;

    public void Dispose() => GameThread.Instance.RestoreGameThread(previousGameThreadId);

    [Fact]
    public void BudgetDefersRemainingWorkAndPreservesOrder()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        var applied = new List<int>();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromTicks(1));
        GameThread.EnqueueSafe(() => applied.Add(1));
        GameThread.EnqueueSafe(() => applied.Add(2));
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1 }, applied);
        Assert.Equal(1, GameThread.Instance.QueueLength);
        budget.Dispose();
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1, 2 }, applied);
    }

    [Fact]
    public void BudgetAndTimeoutBelongToTheirOriginalRuntime()
    {
        var first = new GameThread.QueueContext();
        var second = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(first);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromMilliseconds(8), TimeSpan.FromMinutes(5));
        Assert.Equal(1, GameThread.Instance.FrameDrainLimitCount);
        Assert.Equal(TimeSpan.FromMinutes(5), GameThread.Instance.EffectiveBlockingTimeout);
        using (GameThread.ActivateQueue(second))
        {
            Assert.Equal(0, GameThread.Instance.FrameDrainLimitCount);
            Assert.Equal(GameThread.BlockingTimeout, GameThread.Instance.EffectiveBlockingTimeout);
            budget.Dispose();
            Assert.Equal(0, GameThread.Instance.FrameDrainLimitCount);
        }
        Assert.Equal(0, GameThread.Instance.FrameDrainLimitCount);
        Assert.Equal(GameThread.BlockingTimeout, GameThread.Instance.EffectiveBlockingTimeout);
    }

    [Fact]
    public void NestedPumpDoesNotRunNewWorkAsPartOfOuterFrame()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1));
        var applied = new List<int>();
        GameThread.EnqueueSafe(() =>
        {
            applied.Add(1);
            GameThread.Instance.Update(TimeSpan.Zero);
            GameThread.EnqueueSafe(() => applied.Add(3));
        });
        GameThread.EnqueueSafe(() => applied.Add(2));
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1, 2 }, applied);
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1, 2, 3 }, applied);
    }

    [Fact]
    public void DiscardCancelsOldFrameWithoutConsumingReplacementWork()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1));
        var applied = new List<int>();
        GameThread.EnqueueSafe(() =>
        {
            applied.Add(1);
            GameThread.Instance.DiscardQueuedActions();
            GameThread.EnqueueSafe(() => applied.Add(3));
        });
        GameThread.EnqueueSafe(() => applied.Add(2));
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1 }, applied);
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(new[] { 1, 3 }, applied);
    }

    [Fact]
    public void FailedActionCancelsOriginalWaitersButLeavesLaterWorkQueued()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        GameThread.Instance.MarkGameThread();
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1));
        using var waiter = new EventWaitHandle(false, EventResetMode.ManualReset);
        bool laterRan = false;
        var failed = new GameThread.QueuedAction(() =>
        {
            GameThread.EnqueueSafe(() => laterRan = true);
            throw new InvalidOperationException("failed application");
        }, null, "failure", default);
        var abandoned = new GameThread.QueuedAction(() => Assert.Fail("stale waiter ran"), waiter, "waiter", default);
        queue.queue.Enqueue(failed);
        queue.queue.Enqueue(abandoned);
        Assert.Throws<InvalidOperationException>(() => GameThread.Instance.Update(TimeSpan.Zero));
        Assert.True(waiter.WaitOne(TimeSpan.Zero));
        Assert.Throws<OperationCanceledException>(abandoned.ThrowIfNotExecuted);
        Assert.False(laterRan);
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.True(laterRan);
    }
}
