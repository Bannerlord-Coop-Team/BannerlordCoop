using Common;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Utils;

[Collection(nameof(GameThreadCancellationCollection))]
public sealed class GameThreadCancellationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static GameThreadCancellationTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void Update_SkipsCanceledSessionTask_AlreadyCopiedIntoTheDrainBatch()
    {
        using var cancellation = new CancellationTokenSource();
        using var currentBatch = new GameThreadBlocker();
        using var copiedBatch = new GameThreadBlocker(waitForEntry: false);
        bool staleActionRan = false;

        RunWithCancellation(cancellation.Token, () => staleActionRan = true);

        currentBatch.Release();
        copiedBatch.WaitUntilEntered();
        cancellation.Cancel();
        copiedBatch.Release();
        DrainGameThread();

        Assert.False(staleActionRan);
    }

    [Fact]
    public void BlockingRun_ThrowsOperationCanceledException_WhenItsSessionEndsBeforeDrain()
    {
        using var cancellation = new CancellationTokenSource();
        using var gameThread = new GameThreadBlocker();
        bool staleActionRan = false;
        var blockingRun = Task.Run(() =>
            RunWithCancellation(cancellation.Token, () => staleActionRan = true, blocking: true));
        try
        {
            Assert.True(SpinWait.SpinUntil(() => GameThread.Instance.QueueLength > 0, Timeout));
            cancellation.Cancel();
            var exception = Assert.Throws<AggregateException>(() =>
                Assert.True(blockingRun.Wait(Timeout)));
            Assert.IsType<OperationCanceledException>(exception.InnerException);
        }
        finally
        {
            cancellation.Cancel();
            gameThread.Release();
        }

        DrainGameThread();
        Assert.False(staleActionRan);
    }

    [Fact]
    public void SessionlessWorkDrainedInsideOldSession_DoesNotPassOldSessionToNestedWork()
    {
        using var oldCancellation = new CancellationTokenSource();
        using var oldActionEntered = new ManualResetEventSlim(false);
        using var oldActionCompleted = new ManualResetEventSlim(false);
        using var sessionlessActionCompleted = new ManualResetEventSlim(false);
        bool nestedActionRan = false;
        bool pumpCompleted = false;

        RunWithCancellation(oldCancellation.Token, () =>
        {
            oldActionEntered.Set();
            pumpCompleted = GameThread.WaitWhilePumping(
                () => sessionlessActionCompleted.IsSet,
                DateTime.UtcNow + Timeout);
            oldActionCompleted.Set();
        });

        try
        {
            Wait(oldActionEntered);
            GameThread.Run(() =>
            {
                oldCancellation.Cancel();
                GameThread.Run(() => nestedActionRan = true);
                sessionlessActionCompleted.Set();
            });
            Wait(oldActionCompleted);
        }
        finally
        {
            sessionlessActionCompleted.Set();
            oldActionCompleted.Wait(Timeout);
        }

        Assert.True(pumpCompleted);
        Assert.True(nestedActionRan);
    }

    [Fact]
    public void NewSessionWork_RunsAfterThePreviousSessionWasCanceled()
    {
        using var oldCancellation = new CancellationTokenSource();
        using var newCancellation = new CancellationTokenSource();
        bool newSessionActionRan = false;

        oldCancellation.Cancel();
        RunWithCancellation(newCancellation.Token, () => newSessionActionRan = true, blocking: true);

        Assert.True(newSessionActionRan);
    }

    private static void RunWithCancellation(CancellationToken cancellation, Action action, bool blocking = false)
    {
        using var scope = GameThread.ActivateCancellation(cancellation);
        GameThread.Run(action, blocking);
    }

    private static void Wait(ManualResetEventSlim signal) => Assert.True(signal.Wait(Timeout));
    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    private sealed class GameThreadBlocker : IDisposable
    {
        private readonly ManualResetEventSlim entered = new(false);
        private readonly ManualResetEventSlim release = new(false);

        public GameThreadBlocker(bool waitForEntry = true)
        {
            GameThread.Run(() => { entered.Set(); release.Wait(); });

            if (!waitForEntry || entered.Wait(Timeout)) return;
            release.Set();
            throw new TimeoutException("The game thread did not enter the blocker.");
        }

        public void WaitUntilEntered() => Wait(entered);
        public void Release() => release.Set();

        public void Dispose() => release.Set();
    }
}

[CollectionDefinition(nameof(GameThreadCancellationCollection), DisableParallelization = true)]
public sealed class GameThreadCancellationCollection { }
