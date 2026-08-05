using Common;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Utils;

/// <summary>
/// Covers the guard around each action in <see cref="GameThread.Update"/>'s drain. The engine's tick calls
/// Update, so an escaping exception ends the process; here it would kill the pump thread instead, stranding
/// every later blocking caller until its timeout.
/// </summary>
[Collection(nameof(GameThreadCancellationCollection))]
public sealed class GameThreadGuardTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static GameThreadGuardTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void Update_AfterAQueuedActionThrows_KeepsDrainingTheQueue()
    {
        GameThread.Run(() => throw new InvalidOperationException("queued action failure"));

        // Queued behind the failure, so reaching it at all proves the drain survived.
        AssertPumpStillDrains();
    }

    [Fact]
    public void Run_WhenABlockingActionThrows_ReleasesItsCaller()
    {
        // The wait handle is set even on failure, so this returns instead of waiting out BlockingTimeout.
        // The failure goes to the log, not to the caller.
        var elapsed = Stopwatch.StartNew();
        GameThread.Run(() => throw new InvalidOperationException("blocking action failure"), blocking: true);
        Assert.True(elapsed.Elapsed < Timeout, $"the blocking caller was stalled for {elapsed.Elapsed}");

        AssertPumpStillDrains();
    }

    [Fact]
    public void Run_OnTheGameLoopThread_StillPropagatesToTheCaller()
    {
        // The drain's guard only covers queued work, so an action invoked inline keeps throwing at its
        // caller. Pinned because it is the asymmetry Run's remarks call out, and RunSafe is the way out.
        Exception inlineFailure = null;
        bool runSafeThrew = false;

        GameThread.Run(() =>
        {
            inlineFailure = Record.Exception(
                () => GameThread.Run(() => throw new InvalidOperationException("inline failure")));

            try
            {
                GameThread.RunSafe(() => throw new InvalidOperationException("inline run-safe failure"));
            }
            catch (Exception)
            {
                runSafeThrew = true;
            }
        }, blocking: true);

        Assert.IsType<InvalidOperationException>(inlineFailure);
        Assert.Equal("inline failure", inlineFailure.Message);
        Assert.False(runSafeThrew, "RunSafe must swallow on the inline path too");
    }

    /// <summary>
    /// Marshals a probe onto the game thread and fails if the pump no longer runs it promptly.
    /// </summary>
    private static void AssertPumpStillDrains()
    {
        bool probeRan = false;
        var elapsed = Stopwatch.StartNew();

        GameThread.Run(() => probeRan = true, blocking: true);

        Assert.True(probeRan, "the pump never ran the probe");
        Assert.True(elapsed.Elapsed < Timeout, $"the pump took {elapsed.Elapsed} to run the probe");
    }
}
