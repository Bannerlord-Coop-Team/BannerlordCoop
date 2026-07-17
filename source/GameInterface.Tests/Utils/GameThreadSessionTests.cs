using Common;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Utils;

[Collection(GameThreadSessionCollection.Name)]
public sealed class GameThreadSessionTests
{
    static GameThreadSessionTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void Update_SkipsCanceledSessionTask_AlreadyCopiedIntoTheDrainBatch()
    {
        using var session = new GameThreadSession();
        using var currentBatchEntered = new ManualResetEventSlim(false);
        using var releaseCurrentBatch = new ManualResetEventSlim(false);
        using var copiedBatchEntered = new ManualResetEventSlim(false);
        using var releaseCopiedBatch = new ManualResetEventSlim(false);
        bool staleActionRan = false;

        GameThread.Run(() =>
        {
            currentBatchEntered.Set();
            releaseCurrentBatch.Wait();
        });

        try
        {
            Assert.True(currentBatchEntered.Wait(TimeSpan.FromSeconds(5)));

            GameThread.Run(() =>
            {
                copiedBatchEntered.Set();
                releaseCopiedBatch.Wait();
            });
            using (session.Activate())
            {
                GameThread.Run(() => staleActionRan = true);
            }

            releaseCurrentBatch.Set();
            Assert.True(copiedBatchEntered.Wait(TimeSpan.FromSeconds(5)));

            session.Cancel();
        }
        finally
        {
            releaseCurrentBatch.Set();
            releaseCopiedBatch.Set();
        }

        DrainGameThread();

        Assert.False(staleActionRan);
    }

    [Fact]
    public void BlockingRun_ThrowsOperationCanceledException_WhenItsSessionEndsBeforeDrain()
    {
        using var session = new GameThreadSession();
        using var gameThreadEntered = new ManualResetEventSlim(false);
        using var releaseGameThread = new ManualResetEventSlim(false);
        using var blockingRunCompleted = new ManualResetEventSlim(false);
        Exception? blockingRunException = null;
        bool staleActionRan = false;
        bool networkThreadStarted = false;

        GameThread.Run(() =>
        {
            gameThreadEntered.Set();
            releaseGameThread.Wait();
        });

        var networkThread = new Thread(() =>
        {
            try
            {
                using (session.Activate())
                {
                    GameThread.Run(() => staleActionRan = true, blocking: true);
                }
            }
            catch (Exception exception)
            {
                blockingRunException = exception;
            }
            finally
            {
                blockingRunCompleted.Set();
            }
        });

        try
        {
            Assert.True(gameThreadEntered.Wait(TimeSpan.FromSeconds(5)));
            networkThread.Start();
            networkThreadStarted = true;
            Assert.True(SpinWait.SpinUntil(() => GameThread.Instance.QueueLength > 0, TimeSpan.FromSeconds(5)));

            session.Cancel();

            Assert.True(blockingRunCompleted.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            session.Cancel();
            releaseGameThread.Set();
            if (networkThreadStarted)
            {
                networkThread.Join(TimeSpan.FromSeconds(5));
            }
        }

        DrainGameThread();

        Assert.IsType<OperationCanceledException>(blockingRunException);
        Assert.False(staleActionRan);
    }

    [Fact]
    public void SessionlessWorkDrainedInsideOldSession_DoesNotPassOldSessionToNestedWork()
    {
        using var oldSession = new GameThreadSession();
        using var oldActionEntered = new ManualResetEventSlim(false);
        using var startNestedPump = new ManualResetEventSlim(false);
        using var oldActionCompleted = new ManualResetEventSlim(false);
        using var sessionlessActionCompleted = new ManualResetEventSlim(false);
        bool nestedActionRan = false;
        bool pumpCompleted = false;

        using (oldSession.Activate())
        {
            GameThread.Run(() =>
            {
                oldActionEntered.Set();
                startNestedPump.Wait();
                pumpCompleted = GameThread.WaitWhilePumping(
                    () => sessionlessActionCompleted.IsSet,
                    DateTime.UtcNow + TimeSpan.FromSeconds(5));
                oldActionCompleted.Set();
            });
        }

        try
        {
            Assert.True(oldActionEntered.Wait(TimeSpan.FromSeconds(5)));

            GameThread.Run(() =>
            {
                oldSession.Cancel();
                GameThread.Run(() => nestedActionRan = true);
                sessionlessActionCompleted.Set();
            });
            startNestedPump.Set();

            Assert.True(oldActionCompleted.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            sessionlessActionCompleted.Set();
            startNestedPump.Set();
            oldActionCompleted.Wait(TimeSpan.FromSeconds(5));
        }

        Assert.True(pumpCompleted);
        Assert.True(nestedActionRan);
    }

    [Fact]
    public void NewSessionWork_RunsAfterThePreviousSessionWasCanceled()
    {
        using var oldSession = new GameThreadSession();
        using var newSession = new GameThreadSession();
        bool newSessionActionRan = false;

        oldSession.Cancel();

        using (newSession.Activate())
        {
            GameThread.Run(() => newSessionActionRan = true, blocking: true);
        }

        Assert.True(newSessionActionRan);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}

[CollectionDefinition(GameThreadSessionCollection.Name, DisableParallelization = true)]
public sealed class GameThreadSessionCollection
{
    public const string Name = nameof(GameThreadSessionCollection);
}
