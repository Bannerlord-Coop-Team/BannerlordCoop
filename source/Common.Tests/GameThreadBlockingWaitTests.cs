using Common;
using Common.Network;

namespace Common.Tests;

/// <summary>Verifies how a blocking wait resolves once the game thread claimed or finished its action.</summary>
[Collection(nameof(GameThreadCollection))]
public class GameThreadBlockingWaitTests
{
    [Fact]
    public void CompletedAction_WinsOverACanceledSession()
    {
        using var wait = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var cancellation = new CancellationTokenSource();
        var queued = new GameThread.QueuedAction(() => { }, wait, "completed", default);
        Assert.True(queued.TryBeginRun(out _));
        queued.CompleteExecuted();
        cancellation.Cancel();

        GameThread.WaitForBlockingCompletion(queued, cancellation.Token);
    }

    [Fact]
    public void FailedAction_RethrowsItsExceptionOverACanceledSession()
    {
        using var wait = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var cancellation = new CancellationTokenSource();
        var queued = new GameThread.QueuedAction(() => { }, wait, "failed", default);
        Assert.True(queued.TryBeginRun(out _));
        queued.CompleteFailed(new InvalidTimeZoneException("failed"));
        cancellation.Cancel();

        Assert.Throws<InvalidTimeZoneException>(() => GameThread.WaitForBlockingCompletion(queued, cancellation.Token));
    }

    [Fact]
    public void RunningAction_WithACanceledSession_ThrowsWithoutWaiting()
    {
        using var wait = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var cancellation = new CancellationTokenSource();
        var queued = new GameThread.QueuedAction(() => { }, wait, "running", default);
        Assert.True(queued.TryBeginRun(out _));
        cancellation.Cancel();

        var exception = Assert.Throws<OperationCanceledException>(
            () => GameThread.WaitForBlockingCompletion(queued, cancellation.Token));

        Assert.Contains("will finish on the game thread", exception.Message);
    }

    [Fact]
    public void RunningAction_ThatOutlastsTheSecondWait_TimesOut()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));
        using var wait = new EventWaitHandle(false, EventResetMode.ManualReset);
        var queued = new GameThread.QueuedAction(() => { }, wait, "running", default);
        Assert.True(queued.TryBeginRun(out _));

        var exception = Assert.Throws<TimeoutException>(() => GameThread.WaitForBlockingCompletion(queued, default));

        Assert.Contains("started but did not finish", exception.Message);
    }

    [Fact]
    public void PendingAction_ThatTimesOut_ExpiresAndCannotStart()
    {
        var queue = new GameThread.QueueContext();
        using var active = GameThread.ActivateQueue(queue);
        using var budget = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));
        using var wait = new EventWaitHandle(false, EventResetMode.ManualReset);
        var queued = new GameThread.QueuedAction(() => { }, wait, "pending", default);

        Assert.Throws<TimeoutException>(() => GameThread.WaitForBlockingCompletion(queued, default));

        Assert.False(queued.TryBeginRun(out Action? action));
        Assert.Null(action);
        Assert.Null(queued.Act);
    }

    [Fact]
    public void SecondWait_IsCappedAtTheBlockingTimeoutDuringJoinCatchUp()
    {
        Assert.Equal(GameThread.BlockingTimeout, GameThread.RunningWaitLimit(NetworkJoinLimits.ReplayAppliedTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(200), GameThread.RunningWaitLimit(TimeSpan.FromMilliseconds(200)));
    }
}
