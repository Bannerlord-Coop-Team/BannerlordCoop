using Common;
using Common.Util;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Utils;

/// <summary>
/// Covers <see cref="GameThread.IsGameThread"/> and <see cref="GameThread.WaitWhilePumping"/> — the pump that
/// keeps a blocking caller already on the game-loop thread (e.g. the client's map-event-creation wait) from
/// deadlocking. A bare wait on that thread would stall the queue its completion depends on; WaitWhilePumping
/// drains <see cref="GameThread.Update"/> itself while it waits so the network thread keeps making progress.
/// </summary>
public class GameThreadPumpTests
{
    static GameThreadPumpTests()
    {
        // Coop.Tests starts and continuously pumps a dedicated game-loop thread from a [ModuleInitializer]
        // (TestGameLoopPump); force that initializer to run so the pump is up even when this class runs in
        // isolation (see GenericHandlerThreadingTests for the full rationale). RunModuleConstructor is idempotent.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void IsGameThread_IsTrueOnTheGameLoopThread_AndFalseElsewhere()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        // The xUnit worker thread is not the game-loop thread.
        Assert.False(GameThread.Instance.IsGameThread);

        // A probe marshaled onto the game-loop thread sees itself as the game thread. The probe is blocking,
        // so it has recorded the answer by the time Run returns.
        bool onGameThread = false;
        GameThread.Run(() => onGameThread = GameThread.Instance.IsGameThread, blocking: true);
        Assert.True(onGameThread);
    }

    [Fact]
    public void WaitWhilePumping_ThrowsWhenCalledOffTheGameLoopThread()
    {
        // The xUnit worker thread is not the game-loop thread, so it can't pump; the wait refuses to run.
        Assert.False(GameThread.Instance.IsGameThread);
        Assert.Throws<InvalidOperationException>(
            () => GameThread.WaitWhilePumping(() => true, DateTime.UtcNow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void WaitWhilePumping_ReturnsTrueImmediately_WhenTheConditionAlreadyHolds()
    {
        bool result = false;

        // Must run on the game-loop thread (it owns the pump).
        GameThread.Run(
            () => result = GameThread.WaitWhilePumping(() => true, DateTime.UtcNow + TimeSpan.FromSeconds(1)),
            blocking: true);

        Assert.True(result);
    }

    [Fact]
    public void WaitWhilePumping_ReturnsFalse_WhenTheConditionNeverHoldsBeforeTheDeadline()
    {
        bool result = true;
        TimeSpan elapsed = TimeSpan.Zero;

        GameThread.Run(() =>
        {
            DateTime start = DateTime.UtcNow;
            result = GameThread.WaitWhilePumping(() => false, DateTime.UtcNow + TimeSpan.FromMilliseconds(150));
            elapsed = DateTime.UtcNow - start;
        }, blocking: true);

        Assert.False(result);
        // It waited up to the deadline rather than returning early.
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void WaitWhilePumping_OnTheGameLoopThread_DrainsWorkQueuedWhileItWaits()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        bool ran = false;
        bool waitResult = false;

        // Run the wait ON the game-loop thread, mirroring RequestBlocking running inside the StartBattleInternal
        // prefix. While the wait occupies that thread the dedicated pump loop is paused, so the only thing that
        // can run the queued action is WaitWhilePumping's own pump — i.e. this fails if the pump branch is broken.
        GameThread.Run(() =>
        {
            var enqueued = new ManualResetEventSlim(false);
            var worker = new Thread(() =>
            {
                // Non-blocking: the worker is not the game-loop thread, so this queues rather than runs inline.
                GameThread.Run(() => ran = true);
                enqueued.Set();
            });
            worker.Start();
            enqueued.Wait();

            waitResult = GameThread.WaitWhilePumping(() => ran, DateTime.UtcNow + TimeSpan.FromSeconds(5));

            worker.Join();
        }, blocking: true);

        Assert.True(waitResult, "the wait gave up before its own pump drained the queued action");
        Assert.True(ran, "the action queued while the game thread waited was never drained");
    }

    [Fact]
    public void WaitWhilePumping_DrainsWithPatchesLive_EvenWhenCalledUnderAnAllowedThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        bool? allowedWhileDraining = null;
        bool waitResult = false;
        bool callerAllowanceIntactAfter = false;

        GameThread.Run(() =>
        {
            // The caller holds an allowance on the game-loop thread; the pump must not let drained,
            // unrelated queued work inherit it (which would silence those actions' replication patches).
            using (new AllowedThread())
            {
                var enqueued = new ManualResetEventSlim(false);
                var worker = new Thread(() =>
                {
                    GameThread.Run(() => allowedWhileDraining = AllowedThread.IsThisThreadAllowed());
                    enqueued.Set();
                });
                worker.Start();
                enqueued.Wait();

                waitResult = GameThread.WaitWhilePumping(() => allowedWhileDraining.HasValue, DateTime.UtcNow + TimeSpan.FromSeconds(5));

                worker.Join();
                callerAllowanceIntactAfter = AllowedThread.IsThisThreadAllowed();
            }
        }, blocking: true);

        Assert.True(waitResult, "the queued action was never drained");
        Assert.False(allowedWhileDraining, "the drained action inherited the caller's AllowedThread allowance");
        Assert.True(callerAllowanceIntactAfter, "the caller's allowance was not restored after the pump");
    }
}
